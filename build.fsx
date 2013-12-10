#if BOOT
open Fake
module FB = Fake.Boot
FB.Prepare {
    FB.Config.Default __SOURCE_DIRECTORY__ with
        NuGetDependencies =
            let (!!) x = FB.NuGetDependency.Create x
            [
                !!"FAKE"
                !!"NuGet.Build"
                !!"NuGet.Core"
                !!"NUnit.Runners"
            ]
}
#endif

#load ".build/boot.fsx"

open System.IO
open Fake 
open Fake.AssemblyInfoFile
open Fake.MSBuild

// properties
let projectName = "Fracture"
let version = if isLocalBuild then "0.9." + System.DateTime.UtcNow.ToString("yMMdd") else buildVersion
let projectSummary = "Fracture is an F# based socket implementation for high-speed, high-throughput applications."
let projectDescription = "Fracture is an F# based socket implementation for high-speed, high-throughput applications. It is built on top of SocketAsyncEventArgs, which minimises the memory fragmentation common in the IAsyncResult pattern."
let authors = ["Dave Thomas";"Ryan Riley"]
let mail = "ryan.riley@panesofglass.org"
let homepage = "http://github.com/fractureio/fracture"
let license = "http://github.com/fractureio/fracture/raw/master/LICENSE.txt"

// directories
let buildDir = __SOURCE_DIRECTORY__ @@ "build"
let deployDir = __SOURCE_DIRECTORY__ @@ "deploy"
let packagesDir = __SOURCE_DIRECTORY__ @@ "packages"
let testDir = __SOURCE_DIRECTORY__ @@ "test"
let nugetDir = __SOURCE_DIRECTORY__ @@ "nuget"
let nugetLib = nugetDir @@ "lib/net40"
let template = __SOURCE_DIRECTORY__ @@ "template.html"
let sources = __SOURCE_DIRECTORY__ @@ "src"
let docsDir = __SOURCE_DIRECTORY__ @@ "docs"
let docRoot = getBuildParamOrDefault "docroot" homepage

// tools
let nugetPath = "./.nuget/nuget.exe"
let nunitPath = "./packages/NUnit.Runners.2.6.2/tools"

// files
let appReferences =
    !! "src/**/*.fsproj"

let testReferences =
    !! "tests/**/*.fsproj"

// targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir
               docsDir
               testDir
               deployDir
               nugetDir
               nugetLib]
)

Target "BuildApp" (fun _ ->
    if not isLocalBuild then
        [ Attribute.Version(buildVersion)
          Attribute.Title(projectName)
          Attribute.Description(projectDescription)
          Attribute.Guid("020697d7-24a3-4ce4-a326-d2c7c204ffde")
        ]
        |> CreateFSharpAssemblyInfo "src/fracture/AssemblyInfo.fs"

    MSBuildRelease buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)

Target "BuildTest" (fun _ ->
    MSBuildDebug testDir "Build" testReferences
        |> Log "TestBuild-Output: "
)

Target "Test" (fun _ ->
    let nunitOutput = testDir @@ "TestResults.xml"
    !! (testDir @@ "*.Tests.dll")
        |> NUnit (fun p ->
                    {p with
                        ToolPath = nunitPath
                        DisableShadowCopy = true
                        OutputFile = nunitOutput })
)

Target "CopyLicense" (fun _ ->
    [ "LICENSE.txt" ] |> CopyTo buildDir
)

Target "CreateNuGet" (fun _ ->
    [ buildDir @@ "Fracture.dll"
      buildDir @@ "Fracture.pdb" ]
        |> CopyTo nugetLib

    NuGet (fun p -> 
            {p with               
                Authors = authors
                Project = projectName
                Description = projectDescription
                Version = version
                OutputPath = nugetDir
                ToolPath = nugetPath
                AccessKey = getBuildParamOrDefault "nugetkey" ""
                Publish = hasBuildParam "nugetKey" })
        "fracture.nuspec"

    !! (nugetDir @@ sprintf "Fracture.%s.nupkg" version)
        |> CopyTo deployDir
)

FinalTarget "CloseTestRunner" (fun _ ->
    ProcessHelper.killProcess "nunit-agent.exe"
)

Target "Deploy" DoNothing
Target "Default" DoNothing

// Build order
"Clean"
  ==> "BuildApp" <=> "BuildTest" <=> "CopyLicense"
  ==> "Test"
  ==> "CreateNuGet"
  ==> "Deploy"

"Default" <== ["Deploy"]

// Start build
RunTargetOrDefault "Default"
