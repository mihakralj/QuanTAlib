#!/bin/bash
dotnet gitversion . /output buildserver
dotnet build
