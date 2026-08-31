# Run executable Plugins through a separate Plugin Host

Executable Plugins run one process per Plugin behind `Plana.PluginHost` and communicate through a small versioned local protocol. `Plana.Desktop` never loads third-party DLLs.

The first developer preview uses a current-user named pipe and newline-delimited JSON envelopes. Process separation exists to contain crashes and hangs. Valid discovered Plugins start automatically unless disabled by the user; there is no trust-review, hash-approval, or approval-expiry workflow at this product stage.

This preserves Action Packs as the default extension mechanism. Opening URLs, files, folders, applications, bounded commands, and explicitly interpreted scripts remains simpler and safer as declarative Actions.
