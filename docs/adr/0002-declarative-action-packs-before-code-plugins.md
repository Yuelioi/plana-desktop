# Prefer declarative Action Packs over in-process code plugins

User extensions initially contribute Actions through JSON manifests and host-provided capabilities. Arbitrary .NET assemblies are not loaded into the desktop process because AssemblyLoadContext is an isolation mechanism, not a security sandbox; future executable Plugins must run in a separate Plugin Host process and communicate through a narrow protocol.
