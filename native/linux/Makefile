# Builds libmsbpvdi.so against the SQL Server on Linux VDI client library.
# Run on the SQL Server machine (needs g++ and /opt/mssql/lib/libsqlvdi.so,
# which ships with SQL Server on Linux 2017+).
#
#   make                 # builds ./libmsbpvdi.so
#   make install         # copies it next to the published sqlsqueeze binary
#                        # (pass PUBLISH_DIR=... to override)
#
# SqlSqueeze also finds libmsbpvdi.so in /opt/mssql/lib, /usr/lib and
# /usr/local/lib, so `sudo make SYSINSTALL=1` works too.

CXX ?= g++
SQLVDI_LIB ?= /opt/mssql/lib
PUBLISH_DIR ?= ../../SqlSqueeze.Cli/bin/Release/net10.0/publish

all: libmsbpvdi.so

libmsbpvdi.so: vdishim.cpp vdi.h vdierror.h
	$(CXX) -shared -fPIC -O2 -o $@ vdishim.cpp \
		-L$(SQLVDI_LIB) -lsqlvdi \
		-Wl,-rpath,$(SQLVDI_LIB)

install: libmsbpvdi.so
	mkdir -p $(PUBLISH_DIR)
	cp libmsbpvdi.so $(PUBLISH_DIR)/

clean:
	rm -f libmsbpvdi.so

.PHONY: all install clean
