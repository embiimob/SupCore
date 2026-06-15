// Global using aliases to disambiguate types that are ambiguous when both
// System.Threading / System.Reflection / System.Net.Mime are in scope via
// ImplicitUsings in .NET 8 SDK-style WinForms projects.
global using Timer = System.Windows.Forms.Timer;
global using MethodInvoker = System.Windows.Forms.MethodInvoker;
global using Font = System.Drawing.Font;

// Ensure System.IO types (File, Path, Directory, Stream*) are always visible
// in files that rely on ImplicitUsings but sit in a namespace where a type
// alias could shadow the implicit include.
global using System.IO;
