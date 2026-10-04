# MiniSteam v3 desktop static audit

## v3.0 foundation status

User-side Visual Studio validation already confirmed:

```text
solution build              ✅
39 / 39 backend tests       ✅
desktop login               ✅
Store from API              ✅
Game Details                ✅
Library                     ✅
logout → Login              ✅
```

## v3.1 patch scope

The patch extends only the WPF client and documentation. It consumes API capabilities that already exist in v2.9.

```text
Wishlist                    added
Cart                        added
Checkout                    added
Reviews                     added
Details commerce state      added
```

## Static verification performed in preparation environment

```text
all XAML files well-formed XML        ✅
MiniSteam.slnx well-formed XML        ✅
MiniSteam.Desktop.csproj valid XML    ✅
ASP.NET backend source unchanged      ✅
MiniSteam.Tests source unchanged      ✅
no new EF migration required          ✅
```

The preparation environment does not contain the .NET SDK, therefore C# compilation and WPF XAML compilation must still be confirmed in Visual Studio after copying v3.1.
