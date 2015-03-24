#!/bin/bash

xbuild mono-runtime-bug.sln
set -e
for i in `seq 1 1000`; do mono mono-runtime-bug/bin/Debug/mono-runtime-bug.exe; done

