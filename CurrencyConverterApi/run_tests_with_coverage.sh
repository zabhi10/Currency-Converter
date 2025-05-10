#!/bin/bash

# Ensure we're in the CurrencyConverterApi directory
cd "$(dirname "$0")"

# Clean up previous coverage files
rm -rf coverage-report
rm -f ../CurrencyConverterApi.Tests/coverage.cobertura.xml # Ensure correct XML file is removed
rm -f ../CurrencyConverterApi.Tests/coverage.json # Remove old json file too

# Run the tests with coverage
echo "Running tests with coverage..."
dotnet test ../CurrencyConverterApi.Tests/CurrencyConverterApi.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=../CurrencyConverterApi.Tests/coverage.cobertura.xml /p:Exclude="[xunit*]*%2c[*Tests]*" /p:Threshold=90 /p:ThresholdType=line

# Check if coverage file was created
if [ ! -f "../CurrencyConverterApi.Tests/coverage.cobertura.xml" ]; then
    echo "Error: coverage.cobertura.xml not found at ../CurrencyConverterApi.Tests/coverage.cobertura.xml after running tests."
    exit 1
fi

# Generate HTML coverage report
echo "Generating HTML coverage report..."
# Ensure reportgenerator is installed or use a local tool manifest.
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.3.8 || true # Updated version
~/.dotnet/tools/reportgenerator -reports:"../CurrencyConverterApi.Tests/coverage.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:"Html" -verbosity:Verbose

echo "Coverage report generated in ./coverage-report/index.html"