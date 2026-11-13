#!/bin/sh
# Runs the regression tests. Needs no KSP: only the C# compiler (mcs, or dotnet when mcs is missing).
set -e
cd "$(dirname "$0")"
if command -v mcs >/dev/null 2>&1; then
  out="${TMPDIR:-/tmp}/scribe-tests.exe"
  mcs -nowarn:0649 -out:"$out" ../Source/ScribeCore.cs Regression.cs
  mono "$out"
else
  dotnet run --project Regression.csproj
fi
