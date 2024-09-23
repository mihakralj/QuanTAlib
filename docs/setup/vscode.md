# Development Environment Setup in VS Code

Call me grizzled old man, but I do not like to use [full Visual Studio](https://help.quantower.com/quantower/quantower-algo/installing-visual-studio) environment for my coding work. Here is the setup for VS Code projects for Quantower, so you can build your own as well.

### Prerequisites

- [VS Code](https://code.visualstudio.com/) - obviously
- [.NET SDK](https://dotnet.microsoft.com/en-us/download) - you should probably have this already
- [C# Dev Kit Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) - so VS Code can understand C#
- [C# Base language support Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) - I *think* this is a prereq for C# Dev Kit and will install automatically
- [Polyglot Notebooks Extension](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode) - optional, but really recommended for tinkering with C# code

### Installation Steps

1. Create a new `myIndicator.csproj` file in a directory of your choice - it doesn't have to be anywhere in Quantower directory structure
2. Add all standard elements to `myIndicator.csproj`
3. We need to tell dotnet compiler how to find `TradingPlatform.BusinessLayer.dll` assembly. it is hiding deep in the bowels of Quantower directory structure, including an ever-changing version directory. Luckily msbuild magick can help:

``` XML
<PropertyGroup>
  <!-- Point this element to the directory where Quantower is installed -->
  <QuantowerRoot>D:\Quantower</QuantowerRoot>
  <!-- Find the first directory that starts with "v1" in TradingPlatform  -->
  <QuantowerPath>$([System.IO.Directory]::GetDirectories("$(QuantowerRoot)\TradingPlatform", "v1*")[0])</QuantowerPath>
</PropertyGroup>
<ItemGroup>
  <!-- We need this one so we can compile any classess that use Quantower API -->
  <Reference Include="TradingPlatform.BusinessLayer">
    <HintPath>$(QuantowerPath)\bin\TradingPlatform.BusinessLayer.dll</HintPath>
  </Reference>
  <!-- We need this one so IntelliSense inside VS Code can give us hints -->
  <None Include="$(QuantowerPath)\bin\TradingPlatform.BusinessLayer.xml">
    <Link>TradingPlatform.BusinessLayer.xml</Link>
  </None>
</ItemGroup>
```
4. Each time dotnet compiler creates a new dll assembly, we need to copy it to the `.\Scripts\Indicatiors` directory so Quantower can use it. Let's automate this with a post-build event in our `myIndicator.csproj`:

``` xml
<Target Name="CopyCustomContent" AfterTargets="AfterBuild">
  <!-- after succesful build, the compiled .dll is copied over to Quantower structure  -->
  <Copy SourceFiles="$(OutputPath)\myIndicator.dll" DestinationFolder="$(QuantowerRoot)\Settings\Scripts\Indicators\myIndicator" />
</Target>
```

Below is a sample complete `.csproj` file for a Quantower indicator - it should allow building the .dll assembly and copying it to Quantower structure with `dotnet build` command:

``` xml
﻿<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
    <NeutralLanguage>en-US</NeutralLanguage>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Deterministic>true</Deterministic>
    <LangVersion>preview</LangVersion>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <DisableImplicitNamespaceImports>true</DisableImplicitNamespaceImports>
    <PlatformTarget>AnyCPU</PlatformTarget>
    <AllowUnsafeBlocks>False</AllowUnsafeBlocks>
    <OutputPath>bin\$(Configuration)\</OutputPath>
    <ProduceReferenceAssembly>False</ProduceReferenceAssembly>
    <DebugType>full</DebugType>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PlatformTarget>AnyCPU</PlatformTarget>
  </PropertyGroup>

  <PropertyGroup>
    <QuantowerRoot>D:\Quantower</QuantowerRoot>
    <QuantowerPath>$([System.IO.Directory]::GetDirectories("$(QuantowerRoot)\TradingPlatform", "v1*")[0])</QuantowerPath>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="TradingPlatform.BusinessLayer">
      <HintPath>$(QuantowerPath)\bin\TradingPlatform.BusinessLayer.dll</HintPath>
    </Reference>
    <None Include="$(QuantowerPath)\bin\TradingPlatform.BusinessLayer.xml">
      <Link>TradingPlatform.BusinessLayer.xml</Link>
    </None>
  </ItemGroup>

  <Target Name="CopyCustomContent" AfterTargets="AfterBuild">
     <Copy SourceFiles="$(OutputPath)\myIndicator.dll" DestinationFolder="$(QuantowerRoot)\Settings\Scripts\Indicators\myIndicator" />
    </Target>
</Project>
```