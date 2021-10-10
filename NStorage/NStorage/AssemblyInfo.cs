using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// In SDK-style projects such as this one, several assembly attributes that were historically
// defined in this file are now automatically added during build and populated with
// values defined in project properties. For details of which attributes are included
// and how to customise this process see: https://aka.ms/assembly-info-properties


// Setting ComVisible to false makes the types in this assembly not visible to COM
// components.  If you need to access a type in this assembly from COM, set the ComVisible
// attribute to true on that type.

[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM.

// TODO address the issue with intertop conflicts
//[assembly: Guid("a5891e55-995d-43a6-8894-f23f398b10d0")]
[assembly: InternalsVisibleTo("NStorage.Tests")]
[assembly: InternalsVisibleTo("NStorage.Tests.Benchmarks")]
