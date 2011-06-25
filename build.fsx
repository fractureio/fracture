#I "./packages/FAKE.1.56.7/tools"
#r "FakeLib.dll"

open Fake 
open System.IO

// properties
let projectName = "Fracture"
let version = "0.1.4"
let projectSummary = "Fracture is an F# based socket implementation for high-speed, high-throughput applications."
let projectDescription = "Fracture is an F# based socket implementation for high-speed, high-throughput applications. It is built on top of SocketAsyncEventArgs, which minimises the memory fragmentation common in the IAsyncResult pattern."
let authors = ["Dave Thomas";"Ryan Riley"]
let mail = "ryan.riley@panesofglass.org"
let homepage = "http://github.com/fractureio/fracture"
let license = "http://github.com/fractureio/fracture/raw/master/LICENSE.txt"
let nugetKey = if System.IO.File.Exists "./key.txt" then ReadFileAsString "./key.txt" else ""

// directories
let buildDir = "./build/"
let packagesDir = "./packages/"
let testDir = "./test/"
let deployDir = "./deploy/"
let docsDir = "./docs/"
let nugetDir = "./nuget/"
let targetPlatformDir = getTargetPlatformDir "4.0.30319"
let nugetLibDir = nugetDir @@ "lib"
let nugetDocsDir = nugetDir @@ "docs"
let fparsecVersion = GetPackageVersion packagesDir "FParsec"

// params
let target = getBuildParamOrDefault "target" "All"

// tools
let fakePath = "./packages/FAKE.1.56.7/tools"
let nugetPath = "./lib/Nuget/nuget.exe"
let nunitPath = "./packages/NUnit.2.5.9.10348/Tools"

// files
let appReferences =
    !+ "./src/lib/**/*.fsproj"
        |> Scan

let testReferences =
    !+ "./src/tests/**/*.fsproj"
      |> Scan

let filesToZip =
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir; docsDir]
)

Target "BuildApp" (fun _ ->
    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = projectSummary
            AssemblyDescription = projectDescription
            Guid = "020697d7-24a3-4ce4-a326-d2c7c204ffde"
            OutputFileName = "./src/lib/fracture/AssemblyInfo.fs" })

    AssemblyInfo (fun p ->
        {p with 
            CodeLanguage = FSharp
            AssemblyVersion = version
            AssemblyTitle = "Fracture.Http"
            AssemblyDescription = "An HTTP and URI parser combinator library."
            Guid = "13571762-E1C9-492A-9141-37AA0094759A"
            OutputFileName = "./src/lib/http/AssemblyInfo.fs" })

    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "BuildTest" (fun _ ->
    MSBuildDebug testDir "Build" testReferences
        |> Log "TestBuild-Output: "
)

Target "Test" (fun _ ->
    !+ (testDir + "/*.Tests.dll")
        |> Scan
        |> NUnit (fun p ->
            {p with
                ToolPath = nunitPath
                DisableShadowCopy = true
                OutputFile = testDir + "TestResults.xml" })
)

Target "GenerateDocumentation" (fun _ ->
    !+ (buildDir + "*.dll")
        |> Scan
        |> Docu (fun p ->
            {p with
                ToolPath = fakePath + "/docu.exe"
                TemplatesPath = "./lib/templates"
                OutputPath = docsDir })
)

Target "CopyLicense" (fun _ ->
    [ "LICENSE.txt" ] |> CopyTo buildDir
)

Target "ZipDocumentation" (fun _ ->
    !+ (docsDir + "/**/*.*")
        |> Scan
        |> Zip docsDir (deployDir + sprintf "Documentation-%s.zip" version)
)

Target "BuildNuGet" (fun _ ->
    CleanDirs [nugetDir; nugetLibDir; nugetDocsDir]

    XCopy (docsDir |> FullName) nugetDocsDir
    [ buildDir + "Fracture.dll"
      buildDir + "Fracture.pdb"
      buildDir + "Fracture.Http.dll"
      buildDir + "Fracture.Http.pdb"
      buildDir + "FParsecCS.dll"
      buildDir + "FParsec.dll" ]
        |> CopyTo nugetLibDir

    NuGet (fun p -> 
        {p with               
            Authors = authors
            Project = projectName
            Description = projectDescription
            Version = version
            OutputPath = nugetDir
            Dependencies = ["FParsec",RequireExactly fparsecVersion]
            AccessKey = nugetKey
            ToolPath = nugetPath
            Publish = nugetKey <> "" })
        "fracture.nuspec"

    [nugetDir + sprintf "Fracture.%s.nupkg" version]
        |> CopyTo deployDir
)

Target "Deploy" (fun _ ->
    !+ (buildDir + "/**/*.*")
        -- "*.zip"
        |> Scan
        |> Zip buildDir (deployDir + sprintf "%s-%s.zip" projectName version)
)

Target "All" DoNothing

// Build order
"Clean"
  ==> "BuildApp" <=> "BuildTest" <=> "CopyLicense"
  ==> "Test" <=> "GenerateDocumentation"
  ==> "ZipDocumentation"
  ==> "BuildNuGet"
  ==> "Deploy"

"All" <== ["Deploy"]

// Start build
Run target

