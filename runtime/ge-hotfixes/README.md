# GE-Proton wine hotfixes

Taken from GE-Proton's `patches/wine-hotfixes/pending` (proton-ge-custom `cc7c349`, November 2026); the code of each
patch is unchanged. GE-Proton now relies on ntsync rather than fsync, so these are the parts of its wine that still matter here:
small fixes for performance and stability that are not in Wine yet.

| Patch | Effect |
| --- | --- |
| `0001` ntdll SRWLock | `RtlReleaseSRWLockShared` no longer takes the futex queue lock when nobody waits (5% of the main thread in MSFS 2024). |
| `0002` kernel32 power status | `GetSystemPowerStatus` answers from a cache refreshed in the background instead of blocking on sysfs (removes periodic hitches). |
| `0003` xinput | No wait for the update thread under the loader lock (avoids stalls when a controller connects). |
| `0004` ws2_32 connect | Validates the address passed to `connect()` like Windows (online/multiplayer code paths). |
| `0005` wineserver | Keeps a request's status when a stale fd is dropped. |

`apply.sh` applies each patch that fits the tree and skips the others. `runtime/winegdk/build.sh` (Wine 11) takes all
five; `runtime/wine-uwp/build.sh` (Wine 9.16) takes 0001, 0002, 0003 and 0005.
Wine is LGPL-2.1-or-later; the patches keep their original authors.
