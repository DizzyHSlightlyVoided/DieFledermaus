﻿using System.Resources;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("DieFledermaus")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("Copyright © 2015 by KimikoMuffin")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: NeutralResourcesLanguage("en-US")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

#if NET_3_5
[assembly: AssemblyProduct("DieFledermaus (.Net 3.5)")]
[assembly: AssemblyConfiguration(".Net 3.5")]
#elif NET_4_0
[assembly: AssemblyProduct("DieFledermaus (.Net 4.0)")]
[assembly: AssemblyConfiguration(".Net 4.0")]
#elif NET_4_6
[assembly: AssemblyProduct("DieFledermaus (.Net 4.6)")]
[assembly: AssemblyConfiguration(".Net 4.6")]
#else
[assembly: AssemblyProduct("DieFledermaus (.Net 4.5)")]
[assembly: AssemblyConfiguration(".Net 4.5")]
#endif

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
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
