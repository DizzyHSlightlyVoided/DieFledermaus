using System.Resources;
using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("DieFledermaus")]
[assembly: AssemblyDescription("A library for reading and writing DieFledermaus and DieFledermauZ archives."
    + "WARNING: Format specification in flux. Not suitable for release.")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyCopyright("Copyright © 2015 by KimikoMuffin")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: NeutralResourcesLanguage("en-US")]

#if !PCL_4_0
// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]
#endif

#if ANDROID
[assembly: AssemblyProduct("DieFledermaus (Android)")]
[assembly: AssemblyConfiguration("Android")]
#elif IOS
[assembly: AssemblyProduct("DieFledermaus (iOS)")]
[assembly: AssemblyConfiguration("iOS")]
#elif NET_3_5
[assembly: AssemblyProduct("DieFledermaus (.Net 3.5)")]
[assembly: AssemblyConfiguration(".Net 3.5")]
#elif NET_4_0
[assembly: AssemblyProduct("DieFledermaus (.Net 4.0)")]
[assembly: AssemblyConfiguration(".Net 4.0")]
#elif NET_4_6
[assembly: AssemblyProduct("DieFledermaus (.Net 4.6)")]
[assembly: AssemblyConfiguration(".Net 4.6")]
#elif NET_4_5
[assembly: AssemblyProduct("DieFledermaus (.Net 4.5)")]
[assembly: AssemblyConfiguration(".Net 4.5")]
#elif PCL_4_5
[assembly: AssemblyProduct("DieFledermaus (PCL 4.5)")]
[assembly: AssemblyConfiguration("PCL 4.5")]
#elif PCL_4_0
[assembly: AssemblyProduct("DieFledermaus (PCL 4.0)")]
[assembly: AssemblyConfiguration("PCL 4.0")]
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
[assembly: AssemblyVersion("0.3.9.0")]
[assembly: AssemblyFileVersion("0.3.9.0")]
