﻿using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("RunTemplate")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("RunTemplate")]
[assembly: AssemblyCopyright("")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("3.0.1.0")]
[assembly: AssemblyFileVersion("3.0.1.0")]
#if DEBUG
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Core", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "f75721f2e3128173")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Runtime", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "f75721f2e3128173")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Models", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "f75721f2e3128173")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Models.DbSchema", NewVersion = "3.0.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.0.0", PublicKeyToken = "f75721f2e3128173")]
#else
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Core", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "602c64961bdc076c")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Runtime", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "602c64961bdc076c")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Models", NewVersion = "3.0.1.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.1.0", PublicKeyToken = "602c64961bdc076c")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "CodegenCS.Models.DbSchema", NewVersion = "3.0.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "3.0.0.0", PublicKeyToken = "602c64961bdc076c")]
#endif
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "InterpolatedColorConsole", NewVersion = "1.0.3.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "1.0.3.0", PublicKeyToken = "5d09c7de01a5e5b7")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "NSwag.Core", NewVersion = "13.17.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "13.17.0.0", PublicKeyToken = "c2d88086e098d109")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "NSwag.Core.Yaml", NewVersion = "13.17.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "13.17.0.0", PublicKeyToken = "c2d88086e098d109")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "NJsonSchema", NewVersion = "10.8.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "10.8.0.0", PublicKeyToken = "c2f9c3bdfae56102")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "Namotion.Reflection", NewVersion = "2.1.0.0", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "2.1.0.0", PublicKeyToken = "c2f9c3bdfae56102")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "System.CommandLine", NewVersion = "42.42.42.42", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "42.42.42.42")]
[assembly: Microsoft.VisualStudio.Shell.ProvideBindingRedirection(AssemblyName = "System.CommandLine.NamingConventionBinder", NewVersion = "42.42.42.42", OldVersionLowerBound = "1.0.0.0", OldVersionUpperBound = "42.42.42.42")]


// <InternalsVisibleTo> or <AssemblyAttribute> don't seem to work in legacy project format (non-SDK)
#if (DEBUG || TEST)
[assembly: InternalsVisibleTo("CodegenCS.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f98cb5b97282e4ddf4bb9d07d9860c6741dbc45eb22b46e33d7b57ace52dcb0d07e8b5756958445114ddffaf2101050e58be2e1ce8aa659cc6166bc0d61c9045877790b07571c6007256762fd6d91717827b751c36a613523a870d66e004cc7aa75c2b90e690c313a4ef34d6599264ab1c5e0c44c597f5d36dedfedee1b376d7")]
#else
[assembly: InternalsVisibleTo("CodegenCS.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100897426874fc02e524166ac6278c566d68b71c4f338afc4d24c318729895fd262a45b7d8e2d9c2319732c86407a811c225b4cd2e69bc515415098b505d942fac4af07c8da147a0f1a0a6e0b7c2f6139d2f7113deaf3659ace5bd63668e3d8fe575bb5dc71ee0345be52932fb4b6c6d5def7ebcda841ef7a4495201e008bc95ac5")]
#endif