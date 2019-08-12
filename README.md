# CSharp-InternalExample
C++ only for internals? Hell no!

# Please Note:
You will need an Injector that is capable of injecting managed assemblies into unmanaged ones.
One that works perfectly fine is [ExtremeInjector](https://github.com/master131/ExtremeInjector)

If you use ExtremeInjector, make sure to change the calling convention of your entrypoint to cdecl
(Add MyInjectableLibrary in Extreme injector, press ... next to the name and make the appropriate changes there)

MyInjectableLibrary is built with x86 as its target architecture so make sure to inject it in a 32bit host process (ExtremeInjector will warn you anyways about this)

## Highest Supported .NET Framework version 4.5.2

![alt text](https://i.imgur.com/KSXDXUF.png "Showcase")