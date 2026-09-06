# Third-party components

This independent Windows application uses the following components. Upstream notices and license terms remain applicable; this application is not an official MailKit distribution or endorsement.

- MailKit 4.17.0 — MIT — https://github.com/jstedfast/MailKit
- MimeKit 4.17.0 — MIT — https://github.com/jstedfast/MimeKit
- Microsoft.Data.Sqlite and .NET runtime — MIT — https://github.com/dotnet/efcore / https://github.com/dotnet/runtime
- SQLitePCLRaw — Apache-2.0 — https://github.com/ericsink/SQLitePCL.raw
- SQLite native engine — public domain, subject to the native package's accompanying notices — https://sqlite.org/copyright.html
- BouncyCastle.Cryptography — MIT — https://github.com/bcgit/bc-csharp
- Additional transitive .NET packages — license expressions and versions are recorded in packages.lock.json and their NuGet metadata.
- Installer: NSIS 3.12, using zlib compression — zlib/libpng license (see `licenses/NSIS.txt`) — https://nsis.sourceforge.io/

Full upstream license texts are included under `licenses/`. Self-contained runtime files also retain their distributed notices. Review package licenses again when upgrading dependencies.
