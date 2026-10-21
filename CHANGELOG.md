# Changelog

The package version is major.minor from version.json plus the CI run number, so
every push to main publishes a new patch version. Breaking changes bump the
major version.

## 1.0

- The package embeds its PDB with SourceLink, so the debugger can step into
  the library source (1.0.63 and later).
- Performance: sending sharded by universe and no per-packet allocations on
  the send path.
- A send-immediately method.
- .NET 8, .NET 9 and .NET 10 support.
- Rewritten on the shared Haukcode.HighPerfComm client.
- 1.0.0 - Initial release
