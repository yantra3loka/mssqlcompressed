# Linux VDI shim (`libmsbpvdi.so`)

SqlSqueeze talks to SQL Server through VDI. On Windows that is COM
(`sqlvdi.dll`); on Linux it is C++ classes in
`/opt/mssql/lib/libsqlvdi.so`, which .NET P/Invoke cannot call directly.
This directory builds the tiny C wrapper (`vdishim.cpp`) that bridges them.

## Prereqs (on the SQL Server machine)

- g++ (`sudo apt install g++` / `sudo yum install gcc-c++`)
- SQL Server on Linux 2017+ (provides `/opt/mssql/lib/libsqlvdi.so`)
- This directory (`vdi.h`/`vdierror.h` are vendored from Microsoft's
  [sql-server-samples](https://github.com/Microsoft/sql-server-samples/tree/master/samples/features/sqlvdi-linux)
  (MIT); `vdishim.cpp` + `Makefile` are SqlSqueeze's own, GPL like the rest)

## Build and install

```bash
cd native/linux
make
make install            # into the dotnet publish dir, or:
sudo cp libmsbpvdi.so /opt/mssql/lib/
```

At runtime SqlSqueeze looks for `libmsbpvdi.so` next to its own binary,
then in `/opt/mssql/lib`, `/usr/lib`, `/usr/local/lib`.

## Permissions

VDI uses POSIX shared memory/semaphores shared with SQL Server, so run
SqlSqueeze as the `mssql` user (`sudo su mssql`) or set up the
`mssql`/`vdiuser` groups per [Microsoft's docs](https://learn.microsoft.com/en-us/sql/linux/business-continuity/backup-restore/backup-virtual-device-interface-specification).

## Notes

- SQL Server on Linux has no named instances: the `instancename` db-plugin
  option is accepted but ignored there; connect to the host itself.
- Everything else (pipeline, `gzip`/`bzip2`/`zip64`/`rate`, `local`
  storage, SQL commands) is identical on both OSes.
