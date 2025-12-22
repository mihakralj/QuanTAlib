#!/bin/bash
set -e

echo "Installing .NET 10.0 SDK..."
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --quality daily --install-dir /usr/share/dotnet

echo ".NET 10.0 SDK installed."
dotnet --list-sdks
