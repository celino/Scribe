#!/bin/sh
# Runs the unit tests (layer 2). Needs only the dotnet SDK, no KSP. Extra arguments go to `dotnet test`.
# Coverage: sh tests/run.sh --collect:"XPlat Code Coverage" --results-directory tests/TestResults
set -e
cd "$(dirname "$0")/.."
exec dotnet test tests/Scribe.Tests/Scribe.Tests.csproj "$@"
