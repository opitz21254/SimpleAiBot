#!/bin/bash

# Download the .NET install script
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh

# Make the script executable
chmod +x ./dotnet-install.sh

# Install the .NET SDK for .NET 8.0
./dotnet-install.sh --channel 8.0

# Export environment variables so that dotnet command is available
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools

# Build the project
dotnet publish SimpleBlazor.csproj -c Release -o build
