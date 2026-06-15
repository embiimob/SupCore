// Global using aliases to disambiguate types that are ambiguous when both
// System.Threading and System.Windows.Forms are in scope (ImplicitUsings
// pulls in System.Threading globally for .NET 8 SDK-style projects).
global using Timer = System.Windows.Forms.Timer;
