#!/bin/sh
# Builds the Fracture library.
set -e
"./packages/FAKE.1.64.6/tools/FAKE.exe" "build.fsx"
