using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyInjectableLibrary
{
	public class NativeThread : IDisposable
	{
		public enum NativeThreadState : uint
		{
			THREADSTATE_IDLE_RUNNING = 0x1,
			THREADSTATE_SUSPENDED = 0x2,
			THREADSTATE_ABORTED = 0x3,
		}

		private IntPtr _threadHandle;
		private IntPtr _threadId;

		private bool _typeIsDelegate;
		private bool _hasExecuted = false;

		private ThreadStart _threadStartDelegate;
		private IntPtr _threadStartAddress;
		private IntPtr _threadLpParameter;

		private readonly PInvoke.ThreadCreationFlags _threadCreationFlags;
		private NativeThreadState _threadState = NativeThreadState.THREADSTATE_SUSPENDED;

		// If thread executed, this will get updated to CREATE_RUN_DIRECTLY only
		// if the currentvalue of _threadCreationflags was was not that already
		private PInvoke.ThreadCreationFlags _threadCurrentCreationFlags;

		private uint _ThreadReturnCode = uint.MaxValue;

		// thread context ctx stuff

		public NativeThreadState ThreadState
		{
			get => _threadState;
			set
			{
				if (ThreadState == value) return;
				bool _isSuspended = _threadState == NativeThreadState.THREADSTATE_SUSPENDED || !_hasExecuted;
				if (_isSuspended)
				{
					bool resumeThreadSuccess = PInvoke.ResumeThread(_threadHandle) != -1;
					if (resumeThreadSuccess)
					{
						ThreadState = value;
						return;
					}

					throw new InvalidOperationException("Thread could not be RESUMED (Setting ThreadState field)");
				}
				else
				{
					bool suspendThreadSuccess = PInvoke.SuspendThread(hThread: _threadHandle) != -1;
					if (suspendThreadSuccess)
					{
						_threadState = value;
						return;
					}

					throw new InvalidOperationException("Thread could not be Suspended (Setting ThreadState field)");
				}
			}
		}
		public IntPtr Handle => _threadHandle;
		public int Id => _threadId.ToInt32();
		public uint ReturnCode => _ThreadReturnCode;

		public NativeThread(ThreadStart methodDelegate, IntPtr lpParameter)
		{
			_typeIsDelegate = true;
			_threadStartDelegate = methodDelegate;
			_threadLpParameter = lpParameter;

			IntPtr funcPtr = Marshal.GetFunctionPointerForDelegate(_threadStartDelegate);
			if (funcPtr == IntPtr.Zero) throw new Exception("NativeThread Constructor (Delegate) cannot get address to threadstart delegate");

			_threadStartAddress = funcPtr;
			IntPtr h_thread = PInvoke.CreateThread(IntPtr.Zero, 0, _threadStartAddress, _threadLpParameter, (uint)PInvoke.ThreadCreationFlags.CREATE_SUSPENDED, out _threadId);
			if (h_thread == IntPtr.Zero) throw new InvalidOperationException();

			_threadCreationFlags = PInvoke.ThreadCreationFlags.CREATE_SUSPENDED;
			_threadHandle = h_thread;
		}
		public NativeThread(IntPtr lpStartAddress, IntPtr lpParameter)
		{
			if (lpStartAddress == IntPtr.Zero) throw new InvalidOperationException();

			_typeIsDelegate = false;
			_threadStartAddress = lpStartAddress;
			_threadStartDelegate = null;
			_threadLpParameter = lpParameter;

			IntPtr h_thread = PInvoke.CreateThread(IntPtr.Zero, 0, _threadStartAddress, _threadLpParameter, 0, out _threadId);
			if (h_thread == IntPtr.Zero) throw new InvalidOperationException();

			_threadCreationFlags = PInvoke.ThreadCreationFlags.CREATE_SUSPENDED;
			_threadHandle = h_thread;
		}

		public void Start()
		{
			if (_threadHandle == IntPtr.Zero) return;
			if (_hasExecuted || _threadCreationFlags == PInvoke.ThreadCreationFlags.CREATE_RUN_DIRECTLY) throw new ThreadStateException("Thread has already executed and is idle");

			if (_typeIsDelegate)
			{
				bool resumeThreadWasSuccessfull = PInvoke.ResumeThread(_threadHandle) != -1;
				if (!resumeThreadWasSuccessfull) throw new InvalidOperationException($"ResumeThread(HANDLE) for thread id {_threadId} and thread handle 0x{_threadHandle.ToInt32():X8} failed");

				ThreadState = NativeThreadState.THREADSTATE_IDLE_RUNNING;
				_threadCurrentCreationFlags = PInvoke.ThreadCreationFlags.CREATE_RUN_DIRECTLY;
				_hasExecuted = true;
			}
			else
			{
				IntPtr funcPtr = Marshal.GetFunctionPointerForDelegate(_threadStartDelegate);
				if (funcPtr == IntPtr.Zero) throw new Exception("Cannot start NativeThread (ThreadStart Delegate)");

				bool resumeThreadWasSuccessfull = PInvoke.ResumeThread(_threadHandle) != -1;
				if (!resumeThreadWasSuccessfull) throw new InvalidOperationException($"ResumeThread(HANDLE) for thread id {_threadId} and thread handle 0x{_threadHandle.ToInt32():X8} failed");

				ThreadState = NativeThreadState.THREADSTATE_IDLE_RUNNING;
				_threadCurrentCreationFlags = PInvoke.ThreadCreationFlags.CREATE_RUN_DIRECTLY;
				_hasExecuted = true;
			}
		}

		public bool Join(uint timeout)
		{
			PInvoke.WaitForSingleOBjectResult result = PInvoke.WaitForSingleObject(_threadHandle, timeout);
			switch (result)
			{
				case PInvoke.WaitForSingleOBjectResult.WAIT_OBJECT_O:
					//
					bool getExitCodeSucceeded = PInvoke.GetExitCodeThread(_threadHandle, out uint lpExitCode);
					_ThreadReturnCode = getExitCodeSucceeded ? lpExitCode : uint.MaxValue;
					_hasExecuted = true;
					ThreadState = NativeThreadState.THREADSTATE_IDLE_RUNNING;
					break;
				case PInvoke.WaitForSingleOBjectResult.WAIT_ABANDONED:
					throw new InvalidOperationException("Join() returned WaitForSingleOBjectResult.WAIT_ABANDONED");
					break;
				case PInvoke.WaitForSingleOBjectResult.WAIT_TIMEOUT:
					throw new InvalidOperationException("Join() returned WaitForSingleOBjectResult.WAIT_TIMEOUT");
					break;
			}

			throw new InvalidOperationException("Join() - This should not be seen");
		}

		public bool Abort(int max_retries = 10)
		{
			if (_hasExecuted || (_threadCreationFlags == PInvoke.ThreadCreationFlags.CREATE_SUSPENDED && _threadCurrentCreationFlags == PInvoke.ThreadCreationFlags.CREATE_SUSPENDED) ||
			    ThreadState == NativeThreadState.THREADSTATE_SUSPENDED) return false;

			bool didTerminateThreadSucceed = PInvoke.TerminateThread(_threadHandle, 1337) != 0;
			if (didTerminateThreadSucceed)
			{
				ThreadState = NativeThreadState.THREADSTATE_ABORTED;
				_hasExecuted = true;
				_ThreadReturnCode = 1337;
				return true;
			}
			else
			{
				int _retriesDone = 0;

				for (int n = 0; n < max_retries - 1; n++)
				{
					didTerminateThreadSucceed = PInvoke.TerminateThread(_threadHandle, 1337) != 0;
					if (!didTerminateThreadSucceed) _retriesDone++;
					else
					{
						ThreadState = NativeThreadState.THREADSTATE_ABORTED;
						_hasExecuted = true;
						_ThreadReturnCode = 1337;
						return true;
					}
				}
				
				throw new InvalidOperationException($"Thread refused to terminate after {max_retries} consecutive retries!");
				// return false;
			}
			throw new InvalidOperationException("Abort() - You should not read this");
			// return false;
		}


		public void Dispose()
		{
			_threadId = IntPtr.Zero;
			_threadLpParameter = IntPtr.Zero;
			_threadStartAddress = IntPtr.Zero;
			_threadStartDelegate = null;
			if (_threadHandle != IntPtr.Zero)
			{
				PInvoke.CloseHandle(_threadHandle);
				_threadHandle = IntPtr.Zero;
			}
		}
		~NativeThread()
		{
			// Destructor
			bool success = _threadHandle == IntPtr.Zero || PInvoke.CloseHandle(_threadHandle);
			if (!success)
			{
				success = PInvoke.CloseHandle(_threadHandle);
				if (!success)
				{
					if (ThreadState != NativeThreadState.THREADSTATE_ABORTED)
					{
						Abort();
					}
 				}
			}
			Dispose();
		}
	}
}
