using System;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading;
using static hazedumper.signatures;
using static hazedumper.netvars;
using static MyInjectableLibrary.Enums;
using MyInjectableLibrary;
using Math = System.Math;



namespace MyInjectableLibrary
{
	public class Structs
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void CallbackDelegate(IntPtr structBase);

		[StructLayout(LayoutKind.Sequential, Pack =  1)]
		public unsafe struct Registers
		{
			public IntPtr EAX; // 0
			public IntPtr EBX; // 4
			public IntPtr ECX; // 8
			public IntPtr EDX; // 12
			public IntPtr ESI; // 16
			public IntPtr EDI; // 20
			public IntPtr EBP; // 24

			public IntPtr CodeCaveAddress; // 28
		}

		
		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct D3DMATRIX
		{
			public float _11, _12, _13, _14;
			public float _21, _22, _23, _24;
			public float _31, _32, _33, _34;
			public float _41, _42, _43, _44;

			public float[][] As2DArray()
			{
				return new float[4][]
				{
					new[] { _11, _12, _13, _14 },
					new[] { _21, _22, _23, _24 },
					new[] { _31, _32, _33, _34 },
					new[] { _41, _42, _43, _44 },
				};
			}
			public float[] AsArray()
			{
				return new[]
				{
					_11, _12, _13, _14,
					_21, _22, _23, _24,
					_31, _32, _33, _34,
					_41, _42, _43, _44
				};
			}
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct Vector
		{
			public float X;
			public float Y;

			public Vector(float x, float y)
			{
				X = x;
				Y = y;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Vector4
		{
			public float X;
			public float Y;
			public float Z;
			public float W;
		}

		[StructLayout(LayoutKind.Sequential, Pack =  1)]
		public unsafe struct Vector3
		{
			public Vector3(float x, float y, float z)
			{
				X = x;
				Y = y;
				Z = z;
			}

			public float X;
			public float Y;	
			public float Z;

			public Vector3 Zero => new Vector3(0, 0, 0);

			public bool World2Screen(float[] matrix, out Vector screenPosition)
			{
				if (matrix == null || matrix.Length != 16)
				{
					screenPosition = new Vector(0f, 0f);
					return false;
				}

				Vector4 vec = new Vector4
				{
					X = this.X * matrix[0] + this.Y * matrix[4] + this.Z * matrix[8] + matrix[12],
					Y = this.X * matrix[1] + this.Y * matrix[5] + this.Z * matrix[9] + matrix[13],
					Z = this.X * matrix[2] + this.Y * matrix[6] + this.Z * matrix[10] + matrix[14],
					W = this.X * matrix[3] + this.Y * matrix[7] + this.Z * matrix[11] + matrix[15]
				};

				if (vec.W < 0.1f)
				{
					screenPosition = new Vector(0f, 0f);
					return false;
				}

				Vector3 NDC = new Vector3
				{
					X = vec.X / vec.W,
					Y = vec.Y / vec.W,
					Z = vec.Z / vec.Z
				};

				int* w = (int*)0x00510C94;
				int* h = (int*)0x00510C98;


				screenPosition = new Vector
				{
					X = (*w / 2 * NDC.X) + (NDC.X + *w / 2),
					Y = -(*h / 2 * NDC.Y) + (NDC.Y + *h / 2)
				};
				return true;
			}

			public float Max => (X > Y) ? ((X > Z) ? X : Z) : ((Y > Z) ? Y : Z);
			public float Min => (X < Y) ? ((X < Z) ? X : Z) : ((Y < Z) ? Y : Z);
			public float EuclideanNorm => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
			public float Square => X * X + Y * Y + Z * Z;
			public float Magnitude => (float)Math.Sqrt(SumComponentSqrs());
			public float Distance(Vector3 v1, Vector3 v2)
			{
				return
					(float)Math.Sqrt
					(
						(v1.X - v2.X) * (v1.X - v2.X) +
						(v1.Y - v2.Y) * (v1.Y - v2.Y) +
						(v1.Z - v2.Z) * (v1.Z - v2.Z)
					);
			}
			public float Distance(Vector3 other)
			{
				return Distance(this, other);
			}

			public float Normalize()
			{
				float norm = (float)System.Math.Sqrt(X * X + Y * Y + Z * Z);
				float invNorm = 1.0f / norm;

				X *= invNorm;
				Y *= invNorm;
				Z *= invNorm;

				return norm;
			}
			public Vector3 Inverse()
			{
				return new Vector3(
					(X == 0) ? 0 : 1.0f / X,
					(Y == 0) ? 0 : 1.0f / Y,
					(Z == 0) ? 0 : 1.0f / Z);
			}
			public Vector3 Abs()
			{
				return new Vector3(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
			}
			public Vector3 CrossProduct(Vector3 vector1, Vector3 vector2)
			{
				return new Vector3(
					vector1.Y * vector2.Z - vector1.Z * vector2.Y,
					vector1.Z * vector2.X - vector1.X * vector2.Z,
					vector1.X * vector2.Y - vector1.Y * vector2.X);
			}
			public float DotProduct(Vector3 vector1, Vector3 vector2)
			{
				return vector1.X * vector2.X + vector1.Y * vector2.Y + vector1.Z * vector2.Z;
			}


			public override string ToString()
			{
				return string.Format(CultureInfo.InvariantCulture,
					"{0}, {1}, {2}", X, Y, Z);
			}
			public float[] ToArray()
			{
				return new float[3] { X, Y, Z };
			}

			public float this[int index]
			{
				get
				{
					switch (index)
					{
						case 0: { return X; }
						case 1: { return Y; }
						case 2: { return Z; }
						default: throw new IndexOutOfRangeException($"Range is from 0 to 2");
					}
				}
			}

			public static Vector3 operator +(Vector3 vector, float value)
			{
				return new Vector3(vector.X + value, vector.Y + value, vector.Z + value);
			}
			public static Vector3 operator +(Vector3 vector1, Vector3 vector2)
			{
				return new Vector3(vector1.X + vector2.X, vector1.Y + vector2.Y, vector1.Z + vector2.Z);
			}
			public Vector3 Add(Vector3 vector1, Vector3 vector2)
			{
				return vector1 + vector2;
			}
			public Vector3 Add(Vector3 vector, float value)
			{
				return vector + value;
			}

			private Vector3 SqrComponents(Vector3 v1)
			{
				return
				(
					new Vector3
					(
						v1.X * v1.X,
						v1.Y * v1.Y,
						v1.Z * v1.Z
					)
				);
			}
			private double SumComponentSqrs(Vector3 v1)
			{
				Vector3 v2 = SqrComponents(v1);
				return v2.SumComponents();
			}
			private double SumComponentSqrs()
			{
				return SumComponentSqrs(this);
			}
			private double SumComponents(Vector3 v1)
			{
				return (v1.X + v1.Y + v1.Z);
			}
			private double SumComponents()
			{
				return SumComponents(this);
			}

			public static Vector3 operator -(Vector3 vector1, Vector3 vector2)
			{
				return new Vector3(vector1.X - vector2.X, vector1.Y - vector2.Y, vector1.Z - vector2.Z);
			}
			public Vector3 Subtract(Vector3 vector1, Vector3 vector2)
			{
				return vector1 - vector2;
			}
			public static Vector3 operator -(Vector3 vector, float value)
			{
				return new Vector3(vector.X - value, vector.Y - value, vector.Z - value);
			}
			public Vector3 Subtract(Vector3 vector, float value)
			{
				return vector - value;
			}

			public static Vector3 operator *(Vector3 vector1, Vector3 vector2)
			{
				return new Vector3(vector1.X * vector2.X, vector1.Y * vector2.Y, vector1.Z * vector2.Z);
			}
			public Vector3 Multiply(Vector3 vector1, Vector3 vector2)
			{
				return vector1 * vector2;
			}
			public static Vector3 operator *(Vector3 vector, float factor)
			{
				return new Vector3(vector.X * factor, vector.Y * factor, vector.Z * factor);
			}
			public Vector3 Multiply(Vector3 vector, float factor)
			{
				return vector * factor;
			}

			public static Vector3 operator /(Vector3 vector1, Vector3 vector2)
			{
				return new Vector3(vector1.X / vector2.X, vector1.Y / vector2.Y, vector1.Z / vector2.Z);
			}
			public Vector3 Divide(Vector3 vector1, Vector3 vector2)
			{
				return vector1 / vector2;
			}
			public static Vector3 operator /(Vector3 vector, float factor)
			{
				return new Vector3(vector.X / factor, vector.Y / factor, vector.Z / factor);
			}
			public Vector3 Divide(Vector3 vector, float factor)
			{
				return vector / factor;
			}

			public static bool operator ==(Vector3 vector1, Vector3 vector2)
			{
				return ((vector1.X == vector2.X) && (vector1.Y == vector2.Y) && (vector1.Z == vector2.Z));
			}
			public static bool operator !=(Vector3 vector1, Vector3 vector2)
			{
				return ((vector1.X != vector2.X) || (vector1.Y != vector2.Y) || (vector1.Z != vector2.Z));
			}

			public static bool operator <(Vector3 v1, Vector3 v2)
			{
				return v1.SumComponentSqrs() < v2.SumComponentSqrs();
			}
			public static bool operator <=(Vector3 v1, Vector3 v2)
			{
				return v1.SumComponentSqrs() <= v2.SumComponentSqrs();
			}

			public static bool operator >=(Vector3 v1, Vector3 v2)
			{
				return v1.SumComponentSqrs() >= v2.SumComponentSqrs();
			}
			public static bool operator >(Vector3 v1, Vector3 v2)
			{
				return v1.SumComponentSqrs() > v2.SumComponentSqrs();
			}


			public bool Equals(Vector3 vector)
			{
				return ((vector.X == X) && (vector.Y == Y) && (vector.Z == Z));
			}
			public override bool Equals(object obj)
			{
				if (obj is Vector3 vector3)
				{
					return Equals(vector3);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return X.GetHashCode() + Y.GetHashCode() + Z.GetHashCode();
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Vector3<T> 
		{
			public T X;
			public T Y;
			public T Z;

			
		}
	}
}
