using AzurePipelinesToGitHubActionsConverter.Core;
using AzurePipelinesToGitHubActionsConverter.Core.Conversion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AzurePipelinesToGitHubActionsConverter.Tests
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [TestClass]
    public class CompletePipelineTests
    {
        //Test that the ARM template result includes the AZURE login and download artifacts tasks
        [TestMethod]
        public void ARMTemplatePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
jobs:
- job: Deploy
  displayName: Deploy job
  pool:
    vmImage: ubuntu-latest
  variables:
    AppSettings.Environment: 'data'
    ArmTemplateResourceGroupLocation: 'eu'
    ResourceGroupName: 'MyProjectRG'
  steps:
  - task: DownloadBuildArtifacts@0
    displayName: 'Download the build artifacts'
    inputs:
      buildType: 'current'
      downloadType: 'single'
      artifactName: 'drop'
      downloadPath: '$(build.artifactstagingdirectory)'
  - task: AzureResourceGroupDeployment@2
    displayName: 'Deploy ARM Template to resource group'
    inputs:
      azureSubscription: 'connection to Azure Portal'
      resourceGroupName: $(ResourceGroupName)
      location: '[resourceGroup().location]'
      csmFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.json'
      csmParametersFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.parameters.json'
      overrideParameters: '-environment $(AppSettings.Environment) -locationShort $(ArmTemplateResourceGroupLocation)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets
jobs:
  Deploy:
    name: Deploy job
    runs-on: ubuntu-latest
    env:
      AppSettings.Environment: data
      ArmTemplateResourceGroupLocation: eu
      ResourceGroupName: MyProjectRG
    steps:
    - uses: actions/checkout@v2
    - # ""Note: 'AZURE_SP' secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets""
      name: Azure Login
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_SP }}
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
    - name: Deploy ARM Template to resource group
      uses: Azure/cli@v1.0.0
      with:
        inlineScript: az deployment group create --resource-group ${{ env.ResourceGroupName }} --template-file ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.json --parameters  ${GITHUB_WORKSPACE}/drop/ARMTemplates/azuredeploy.parameters.json -environment ${{ env.AppSettings.Environment }} -locationShort ${{ env.ArmTemplateResourceGroupLocation }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Check that the results include the Setup Java step
        [TestMethod]
        public void AndroidPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/android.yml
            string yaml = @"
# Android
# Build your Android project with Gradle.
# Add steps that test, sign, and distribute the APK, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/android

trigger:
- master

pool:
  vmImage: 'macos-latest'

steps:
- task: Gradle@2
  inputs:
    workingDirectory: ''
    gradleWrapperFile: 'gradlew'
    gradleOptions: '-Xmx3072m'
    publishJUnitResults: false
    testResultsFiles: '**/TEST-*.xml'
    tasks: 'assembleDebug'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: chmod +x gradlew
    - run: ./gradlew build
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Test that the result includes the setup Java step
        [TestMethod]
        public void AntPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: Ant@1
  inputs:
    workingDirectory: ''
    buildFile: 'build.xml'
    javaHomeOption: 'JDKVersion'
    jdkVersionOption: '1.8'
    jdkArchitectureOption: 'x64'
    publishJUnitResults: true
    testResultsFiles: '**/TEST -*.xml'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: ant -noinput -buildfile build.xml
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void DotNetDesktopPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/microsoft/azure-pipelines-yaml/blob/master/templates/.net-desktop.yml
            string yaml = @"
# .NET Desktop
# Build and run tests for .NET Desktop or Windows classic desktop solutions.
# Add steps that publish symbols, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/apps/windows/dot-net

trigger:
- master

pool:
  vmImage: 'windows-latest'

variables:
  solution: 'WindowsFormsApp1.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

steps:
- task: NuGetToolInstaller@1
- task: NuGetCommand@2
  inputs:
    restoreSolution: '$(solution)'
- task: VSBuild@1
  inputs:
    solution: '$(solution)'
    platform: '$(buildPlatform)'
    configuration: '$(buildConfiguration)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  solution: WindowsFormsApp1.sln
  buildPlatform: Any CPU
  buildConfiguration: Release
jobs:
  build:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v2
    - uses: microsoft/setup-msbuild@v1.0.0
    - # 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  ${{ env.solution }}
      shell: powershell
    - run: msbuild '${{ env.solution }}' /p:configuration='${{ env.buildConfiguration }}' /p:platform='${{ env.buildPlatform }}'
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void GoPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/go.yml
            string yaml = @"
# Go
# Build your Go project.
# Add steps that test, save build artifacts, deploy, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/go

trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

variables:
  GOBIN:  '$(GOPATH)/bin' # Go binaries path
  GOROOT: '/usr/local/go1.11' # Go installation path
  GOPATH: '$(system.defaultWorkingDirectory)/gopath' # Go workspace path
  modulePath: '$(GOPATH)/src/github.com/$(build.repository.name)' # Path to the module's code

steps:
- script: |
    mkdir -p '$(GOBIN)'
    mkdir -p '$(GOPATH)/pkg'
    mkdir -p '$(modulePath)'
    shopt -s extglob
    shopt -s dotglob
    mv !(gopath) '$(modulePath)'
    echo '##vso[task.prependpath]$(GOBIN)'
    echo '##vso[task.prependpath]$(GOROOT)/bin'
  displayName: 'Set up the Go workspace'

- script: |
    go version
    go get -v -t -d ./...
    if [ -f Gopkg.toml ]; then
        curl https://raw.githubusercontent.com/golang/dep/master/install.sh | sh
        dep ensure
    fi
    go build -v .
  workingDirectory: '$(modulePath)'
  displayName: 'Get dependencies, then build'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  GOBIN: ${{ env.GOPATH }}/bin
  GOROOT: /usr/local/go1.11
  GOPATH: ${{ env.system.defaultWorkingDirectory }}/gopath
  modulePath: ${{ env.GOPATH }}/src/github.com/${{ env.build.repository.name }}
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Set up the Go workspace
      run: |
        mkdir -p '${{ env.GOBIN }}'
        mkdir -p '${{ env.GOPATH }}/pkg'
        mkdir -p '${{ env.modulePath }}'
        shopt -s extglob
        shopt -s dotglob
        mv !(gopath) '${{ env.modulePath }}'
        echo '##vso[task.prependpath]${{ env.GOBIN }}'
        echo '##vso[task.prependpath]${{ env.GOROOT }}/bin'
    - name: Get dependencies, then build
      run: |
        go version
        go get -v -t -d ./...
        if [ -f Gopkg.toml ]; then
            curl https://raw.githubusercontent.com/golang/dep/master/install.sh | sh
            dep ensure
        fi
        go build -v .
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Check that the results include the setup java step
        [TestMethod]
        public void MavenPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: Maven@3
  inputs:
    mavenPomFile: 'Maven/pom.xml'
    mavenOptions: '-Xmx3072m'
    javaHomeOption: 'JDKVersion'
    jdkVersionOption: '1.8'
    jdkArchitectureOption: 'x64'
    publishJUnitResults: true
    testResultsFiles: '**/surefire-reports/TEST-*.xml'
    goals: 'package'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup JDK 1.8
      uses: actions/setup-java@v1
      with:
        java-version: 1.8
    - run: mvn -B package --file Maven/pom.xml
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Check that the results include the setup Node step
        [TestMethod]
        public void NodeJSPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: NodeTool@0
  inputs:
    versionSpec: '10.x'
  displayName: 'Install Node.js'

- script: |
    npm install
    npm start
  displayName: 'npm install and start'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Install Node.js
      uses: actions/setup-node@v1
      with:
        node-version: 10.x
    - name: npm install and start
      run: |
        npm install
        npm start
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void NuGetPackagePipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
resources:
- repo: self
  containers:
  - container: test123

trigger:
- master

pool:
  vmImage: 'windows-latest'

variables:
  BuildConfiguration: 'Release'
  BuildPlatform : 'Any CPU'
  BuildVersion: 1.1.$(Build.BuildId)

steps:
- task: DotNetCoreCLI@2
  displayName: Restore
  inputs:
    command: restore
    projects: MyProject/MyProject.Models/MyProject.Models.csproj

- task: DotNetCoreCLI@2
  displayName: Build
  inputs:
    projects: MyProject/MyProject.Models/MyProject.Models.csproj
    arguments: '--configuration $(BuildConfiguration)'

- task: DotNetCoreCLI@2
  displayName: Publish
  inputs:
    command: publish
    publishWebProjects: false
    projects: MyProject/MyProject.Models/MyProject.Models.csproj
    arguments: '--configuration $(BuildConfiguration) --output $(build.artifactstagingdirectory)'
    zipAfterPublish: false

- task: DotNetCoreCLI@2
  displayName: 'dotnet pack'
  inputs:
    command: pack
    packagesToPack: MyProject/MyProject.Models/MyProject.Models.csproj
    versioningScheme: byEnvVar
    versionEnvVar: BuildVersion

- task: PublishBuildArtifacts@1
  displayName: 'Publish Artifact'
  inputs:
    PathtoPublish: '$(build.artifactstagingdirectory)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  BuildConfiguration: Release
  BuildPlatform: Any CPU
  BuildVersion: 1.1.${{ env.Build.BuildId }}
jobs:
  build:
    runs-on: windows-latest
    container: {}
    steps:
    - uses: actions/checkout@v2
    - name: Restore
      run: dotnet restore MyProject/MyProject.Models/MyProject.Models.csproj
    - name: Build
      run: dotnet MyProject/MyProject.Models/MyProject.Models.csproj --configuration ${{ env.BuildConfiguration }}
    - name: Publish
      run: dotnet publish MyProject/MyProject.Models/MyProject.Models.csproj --configuration ${{ env.BuildConfiguration }} --output ${GITHUB_WORKSPACE}
    - name: dotnet pack
      run: dotnet pack MyProject/MyProject.Models/MyProject.Models.csproj
    - name: Publish Artifact
      uses: actions/upload-artifact@v2
      with:
        path: ${GITHUB_WORKSPACE}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Check that the results include the setup Python step
        [TestMethod]
        public void PythonPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/python-django.yml
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'
strategy:
  matrix:
    Python35:
      PYTHON_VERSION: '3.5'
    Python36:
      PYTHON_VERSION: '3.6'
    Python37:
      PYTHON_VERSION: '3.7'
  maxParallel: 3

steps:
- task: UsePythonVersion@0
  inputs:
    versionSpec: '$(PYTHON_VERSION)'
    addToPath: true
    architecture: 'x64'
- task: PythonScript@0
  inputs:
    scriptSource: 'filePath'
    scriptPath: 'Python/Hello.py'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        PYTHON_VERSION:
        - 3.5
        - 3.6
        - 3.7
      max-parallel: 3
    steps:
    - uses: actions/checkout@v2
    - name: Setup Python ${{ matrix.PYTHON_VERSION }}
      uses: actions/setup-python@v1
      with:
        python-version: ${{ matrix.PYTHON_VERSION }}
    - run: python Python/Hello.py
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void ResourcesContainersPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-16.04'

container: 'mcr.microsoft.com/dotnet/core/sdk:2.2'

resources:
  containers:
  - container: redis
    image: redis
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#TODO: Container conversion not yet done: https://github.com/samsmithnz/AzurePipelinesToGitHubActionsConverter/issues/39
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-16.04
    container:
      image: redis
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //Test that the result includes the setup Ruby step
        [TestMethod]
        public void RubyPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseRubyVersion@0
  inputs:
    versionSpec: '>= 2.5'
- script: ruby HelloWorld.rb
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup Ruby >= 2.5
      uses: actions/setup-ruby@v1
      with:
        ruby-version: '>= 2.5'
    - run: ruby HelloWorld.rb
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void TestHTMLPipeline()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
# HTML
# Archive your static HTML project and save it with the build record.

trigger:
- master

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: ArchiveFiles@2
  inputs:
    rootFolderOrFile: '$(build.sourcesDirectory)'
    includeRootFolder: false
- task: PublishBuildArtifacts@1";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/marketplace/actions/create-zip-file
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - # 'Note: This is a third party action: https://github.com/marketplace/actions/create-zip-file'
      uses: montudor/action-zip@v0.1.0
      with:
        args: zip -qq -r  ${{ env.build.sourcesDirectory }}
    - uses: actions/upload-artifact@v2
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void TestJobsWithAzurePipelineYamlToObject()
        {
            //Arrange
            Conversion conversion = new Conversion();
            string yaml = @"
trigger:
- master
variables:
  buildConfiguration: Release
  vmImage: ubuntu-latest
jobs:
- job: Build
  displayName: Build job
  pool: 
    vmImage: ubuntu-latest
  timeoutInMinutes: 23
  variables:
    buildConfiguration: Debug
    myJobVariable: 'data'
    myJobVariable2: 'data2'
  steps: 
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 1
- job: Build2
  displayName: Build job
  dependsOn: Build
  pool: 
    vmImage: ubuntu-latest
  variables:
    myJobVariable: 'data'
  steps:
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 2
  - script: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration $(buildConfiguration) 
    displayName: dotnet build part 3";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
on:
  push:
    branches:
    - master
env:
  buildConfiguration: Release
  vmImage: ubuntu-latest
jobs:
  Build:
    name: Build job
    runs-on: ubuntu-latest
    timeout-minutes: 23
    env:
      buildConfiguration: Debug
      myJobVariable: data
      myJobVariable2: data2
    steps:
    - uses: actions/checkout@v2
    - name: dotnet build part 1
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
  Build2:
    name: Build job
    runs-on: ubuntu-latest
    needs:
    - Build
    env:
      myJobVariable: data
    steps:
    - uses: actions/checkout@v2
    - name: dotnet build part 2
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
    - name: dotnet build part 3
      run: dotnet build WebApplication1/WebApplication1.Service/WebApplication1.Service.csproj --configuration ${{ env.buildConfiguration }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void XamarinAndroidPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: 
            string yaml = @"
# Xamarin.Android
# Build a Xamarin.Android project.
# Add steps that test, sign, and distribute an app, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/xamarin

trigger:
- master

pool:
  vmImage: 'macos-latest'

variables:
  buildConfiguration: 'Release'
  outputDirectory: '$(build.binariesDirectory)/$(buildConfiguration)'

steps:
- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '**/*.sln'

- task: XamarinAndroid@1
  inputs:
    projectFile: '**/*droid*.csproj'
    outputDirectory: '$(outputDirectory)'
    configuration: '$(buildConfiguration)'
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  buildConfiguration: Release
  outputDirectory: ${{ env.build.binariesDirectory }}/${{ env.buildConfiguration }}
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - # 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  **/*.sln
      shell: powershell
    - run: |
        cd Blank
        nuget restore
        cd Blank.Android
        msbuild **/*droid*.csproj /verbosity:normal /t:Rebuild /p:Configuration=${{ env.buildConfiguration }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        //TODO: Move to step, doesn't need to be here.
        [TestMethod]
        public void XamariniOSPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/xamarin.ios.yml
            string yaml = @"
# Xamarin.iOS
# Build a Xamarin.iOS project.
# Add steps that install certificates, test, sign, and distribute an app, save build artifacts, and more:
# https://docs.microsoft.com/azure/devops/pipelines/languages/xamarin

trigger:
- master

pool:
  vmImage: 'macos-latest'

steps:
# To manually select a Xamarin SDK version on the Microsoft-hosted macOS agent,
# configure this task with the *Mono* version that is associated with the
# Xamarin SDK version that you need, and set the ""enabled"" property to true.
# See https://go.microsoft.com/fwlink/?linkid=871629
- script: sudo $AGENT_HOMEDIRECTORY/scripts/select-xamarin-sdk.sh 5_12_0
  displayName: 'Select the Xamarin SDK version'
  enabled: false

- task: NuGetToolInstaller@1

- task: NuGetCommand@2
  inputs:
    restoreSolution: '**/*.sln'

- task: XamariniOS@2
  inputs:
    solutionFile: '**/*.sln'
    configuration: 'Release'
    buildForSimulator: true
    packageApp: false
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
jobs:
  build:
    runs-on: macos-latest
    steps:
    - uses: actions/checkout@v2
    - name: Select the Xamarin SDK version
      run: sudo $AGENT_HOMEDIRECTORY/scripts/select-xamarin-sdk.sh 5_12_0
    - # 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  **/*.sln
      shell: powershell
    - run: |
        cd Blank
        nuget restore
        cd Blank.Android
        msbuild  /verbosity:normal /t:Rebuild /p:Platform=iPhoneSimulator /p:Configuration=Release
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }

        [TestMethod]
        public void PipelineWithWorkspaceAndTemplateStepTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://raw.githubusercontent.com/microsoft/azure-pipelines-yaml/master/templates/xamarin.ios.yml
            string yaml = @"
name: $(Version).$(rev:r)

variables:
- group: Common Netlify

trigger:
  branches:
    include:
    - dev
    - feature/*
    - hotfix/*
  paths:
    include:
    - 'Netlify/*'
    exclude:
    - 'pipelines/*'
    - 'scripts/*'
    - '.editorconfig'
    - '.gitignore'
    - 'README.md'

stages:
# Build Pipeline
- stage: Build
  jobs:
  - job: HostedVs2017
    displayName: Hosted VS2017
    pool:
      name: Hosted VS2017
      demands: npm
    workspace:
      clean: all
    
    steps:
    - template: templates/npm-build-steps.yaml
      parameters:
        extensionName: $(ExtensionName)
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#There is no conversion path for templates, currently there is no support to call other actions/yaml files from a GitHub Action
name: ${{ env.Version }}.${GITHUB_RUN_NUMBER}
on:
  push:
    branches:
    - dev
    - feature/*
    - hotfix/*
    paths:
    - Netlify/*
    paths-ignore:
    - pipelines/*
    - scripts/*
    - .editorconfig
    - .gitignore
    - README.md
env:
  group: Common Netlify
jobs:
  Build_Stage_HostedVs2017:
    name: Hosted VS2017
    runs-on: Hosted VS2017
    steps:
    - uses: actions/checkout@v2
    - # There is no conversion path for templates, currently there is no support to call other actions/yaml files from a GitHub Action
      run: |
        #templates/npm-build-steps.yaml
        extensionName: ${{ env.ExtensionName }}
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
        }


        [TestMethod]
        public void JRPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/samsmithnz/AzurePipelinesToGitHubActionsConverter/issues/128
            string yaml = @"
trigger:
- master

pool: 'Pipeline-Demo-Windows'


variables:
  solution: '**/*.sln'
  buildPlatform: 'Any CPU'
  buildConfiguration: 'Release'

stages:

- stage: Build
  jobs: 
    - job: BuildSpark
      pool:
        name: 'Pipeline-Demo-Windows'
        demands:
        - Agent.OS -equals Windows_NT
      steps:
      - task: NuGetToolInstaller@1

      - task: NuGetCommand@2
        inputs:
          restoreSolution: '$(solution)'

      - task: VSBuild@1
        inputs:
          solution: '$(solution)'
          msbuildArgs: '/p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:SkipInvalidConfigurations=true /p:PackageLocation=""$(build.artifactStagingDirectory)""'
          platform: '$(buildPlatform)'
          configuration: '$(buildConfiguration)'

      - task: PublishPipelineArtifact@1
        inputs:
          targetPath: '$(build.artifactStagingDirectory)'
          artifact: 'WebDeploy'
          publishLocation: 'pipeline'

- stage: Deploy
  jobs:
    - deployment: 
      variables:
        Art: ""Server=.;Database=Art;Trusted_Connection=True;""
        #- name: Art
        #  value: ""Server=.;Database=Art;Trusted_Connection=True;""
        
      environment: 
        name: windows-server
        resourceType: VirtualMachine
        tags: web
      strategy:
        runOnce:
          deploy:
            steps:
              - task: DownloadPipelineArtifact@2
                inputs:
                  buildType: 'current'
                  artifactName: 'WebDeploy'
                  targetPath: '$(Pipeline.Workspace)'


              - task: CmdLine@2
                inputs:
                  script: |
                    echo Write your commands here
                    
                    DIR
                  workingDirectory: '$(Pipeline.Workspace)'
                  
              - task: IISWebAppManagementOnMachineGroup@0
                inputs:
                  IISDeploymentType: 'IISWebsite'
                  ActionIISWebsite: 'CreateOrUpdateWebsite'
                  WebsiteName: 'Spark'
                  WebsitePhysicalPath: '%SystemDrive%\inetpub\wwwroot'
                  WebsitePhysicalPathAuth: 'WebsiteUserPassThrough'
                  AddBinding: true
                  CreateOrUpdateAppPoolForWebsite: true
                  ConfigureAuthenticationForWebsite: true
                  AppPoolNameForWebsite: 'Spark'
                  DotNetVersionForWebsite: 'v4.0'
                  PipeLineModeForWebsite: 'Integrated'
                  AppPoolIdentityForWebsite: 'ApplicationPoolIdentity'
                  AnonymousAuthenticationForWebsite: true
                  WindowsAuthenticationForWebsite: false
                  protocol: 'http' 
                  iPAddress: 'All Unassigned'
                  port: '80'
                  
              - task: IISWebAppDeploymentOnMachineGroup@0
                inputs:
                  WebSiteName: 'Spark'
                  Package: '$(Pipeline.Workspace)\Art.Web.zip'
                  XmlVariableSubstitution: true
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: Error! This step does not have a conversion path yet: IISWebAppDeploymentOnMachineGroup@0
#Note: Error! This step does not have a conversion path yet: IISWebAppManagementOnMachineGroup@0
#Note: Error! This step does not have a conversion path yet: DownloadPipelineArtifact@2
#Note: Azure DevOps strategy>runOnce>deploy does not have an equivalent in GitHub Actions yetNote: Azure DevOps job environment does not have an equivalent in GitHub Actions yet
#Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget
on:
  push:
    branches:
    - master
env:
  solution: '**/*.sln'
  buildPlatform: Any CPU
  buildConfiguration: Release
jobs:
  Build_Stage_BuildSpark:
    runs-on: Pipeline-Demo-Windows
    steps:
    - uses: actions/checkout@v2
    - uses: microsoft/setup-msbuild@v1.0.0
    - # 'Note: This is a third party action: https://github.com/warrenbuckley/Setup-Nuget'
      uses: warrenbuckley/Setup-Nuget@v1
    - run: nuget  ${{ env.solution }}
      shell: powershell
    - run: msbuild '${{ env.solution }}' /p:configuration='${{ env.buildConfiguration }}' /p:platform='${{ env.buildPlatform }}' /p:DeployOnBuild=true /p:WebPublishMethod=Package /p:PackageAsSingleFile=true /p:SkipInvalidConfigurations=true /p:PackageLocation=""${{ env.build.artifactStagingDirectory }}""
    - uses: actions/upload-artifact@v2
      with:
        path: ${{ env.build.artifactStagingDirectory }}
  Deploy_Stage_job1:
    # 'Note: Azure DevOps strategy>runOnce>deploy does not have an equivalent in GitHub Actions yetNote: Azure DevOps job environment does not have an equivalent in GitHub Actions yet'
    env:
      Art: Server=.;Database=Art;Trusted_Connection=True;
    steps:
    - # 'Note: Error! This step does not have a conversion path yet: DownloadPipelineArtifact@2'
      run: 'Write-Host Note: Error! This step does not have a conversion path yet: DownloadPipelineArtifact@2 #task: DownloadPipelineArtifact@2#inputs:#  buildtype: current#  artifactname: WebDeploy#  targetpath: ${{ env.Pipeline.Workspace }}'
      shell: powershell
    - run: |
        echo Write your commands here

        DIR
      shell: cmd
    - # 'Note: Error! This step does not have a conversion path yet: IISWebAppManagementOnMachineGroup@0'
      run: ""Write-Host Note: Error! This step does not have a conversion path yet: IISWebAppManagementOnMachineGroup@0 #task: IISWebAppManagementOnMachineGroup@0#inputs:#  iisdeploymenttype: IISWebsite#  actioniiswebsite: CreateOrUpdateWebsite#  websitename: Spark#  websitephysicalpath: '%SystemDrive%\\inetpub\\wwwroot'#  websitephysicalpathauth: WebsiteUserPassThrough#  addbinding: true#  createorupdateapppoolforwebsite: true#  configureauthenticationforwebsite: true#  apppoolnameforwebsite: Spark#  dotnetversionforwebsite: v4.0#  pipelinemodeforwebsite: Integrated#  apppoolidentityforwebsite: ApplicationPoolIdentity#  anonymousauthenticationforwebsite: true#  windowsauthenticationforwebsite: false#  protocol: http#  ipaddress: All Unassigned#  port: 80""
      shell: powershell
    - # 'Note: Error! This step does not have a conversion path yet: IISWebAppDeploymentOnMachineGroup@0'
      run: 'Write-Host Note: Error! This step does not have a conversion path yet: IISWebAppDeploymentOnMachineGroup@0 #task: IISWebAppDeploymentOnMachineGroup@0#inputs:#  websitename: Spark#  package: ${{ env.Pipeline.Workspace }}\Art.Web.zip#  xmlvariablesubstitution: true'
      shell: powershell
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
            Assert.IsTrue(gitHubOutput.actionsYaml != null);
            Assert.IsTrue(gitHubOutput.actionsYaml != "");
        }

        [TestMethod]
        public void SSParentPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/samsmithnz/AzurePipelinesToGitHubActionsConverter/issues/128
            string yaml = @"
variables:
- group: 'myapp KeyVault'
- name: vmImage #Note this weird name/value syntax if you need to reference a variable group in variables
  value: 'windows-latest'

stages:
- stage: DeployPR
  displayName: 'Deploy PR Stage'
  condition: and(succeeded(), eq(variables['Build.Reason'], 'PullRequest'), ne(variables['System.PullRequest.PullRequestId'], 'Null'))
  dependsOn: Build
  variables:
    ${{ if ne(variables['Build.SourceBranchName'], 'master') }}:
      prId: ""$(System.PullRequest.PullRequestId)""
    ${{ if eq(variables['Build.SourceBranchName'], 'master') }}:
      prId: '000'
    prUC: ""PR$(prId)""
    prLC: ""pr$(prId)""
  jobs:
  - template: azure-pipelines-deployment-template.yml
    parameters:
      #Note that pull request environments use Dev credentials
      applicationInsightsApiKey: '$(ApplicationInsights--APIKeyDev)'
      applicationInsightsApplicationId: '$(ApplicationInsights--ApplicationIdDev)'
      applicationInsightsInstrumentationKey: $(ApplicationInsights--InstrumentationKeyDev)
      applicationInsightsLocation: 'East US'
      appServiceContributerClientSecret: $(appServiceContributerClientSecret)
      ASPNETCOREEnvironmentSetting: 'Development'
      captureStartErrors: true
      cognitiveServicesSubscriptionKey: $(cognitiveServicesSubscriptionKey)
      environment: $(prUC)
      environmentLowercase: $(prLC)
      databaseLoginName: $(databaseLoginNameDev) 
      databaseLoginPassword: $(databaseLoginPasswordDev)
      databaseServerName: 'myapp-$(prLC)-eu-sqlserver'
      godaddy_key: $(GoDaddyAPIKey)
      godaddy_secret: $(GoDaddyAPISecret)
      keyVaultClientId: '$(KeyVaultClientId)'
      keyVaultClientSecret: '$(KeyVaultClientSecret)'
      imagesStorageCDNURL: 'https://myapp-$(prLC)-eu-cdnendpoint.azureedge.net/'
      imagesStorageURL: 'https://myapp$(prLC)eustorage.blob.core.windows.net/'
      redisCacheConnectionString: '$(AppSettings--RedisCacheConnectionStringDev)'
      resourceGroupName: 'myapp$(prUC)'
      resourceGroupLocation: 'East US'
      resourceGroupLocationShort: 'eu'
      myappConnectionString: '$(ConnectionStrings--myappConnectionStringDev)'
      serviceName: 'myapp-$(prLC)-eu-service'
      serviceStagingUrl: 'https://myapp-$(prLC)-eu-service-staging.azurewebsites.net/'
      serviceUrl: 'https://myapp-$(prLC)-eu-service.azurewebsites.net/'
      storageAccountName: 'myapp$(prLC)eustorage'
      storageAccountKey: '$(StorageAccountKeyProd)'
      userPrincipalLogin: $(userPrincipalLogin)
      vmImage: $(vmImage)
      websiteName: 'myapp-$(prLC)-eu-web'
      websiteDomainName: '$(prLC).myapp.com'
      websiteStagingUrl: 'https://myapp-$(prLC)-eu-web-staging.azurewebsites.net/'
      websiteUrl: 'https://myapp-$(prLC)-eu-web.azurewebsites.net/'   
";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: Azure DevOps template does not have an equivalent in GitHub Actions yet
env:
  group: myapp KeyVault
  vmImage: windows-latest
jobs:
  DeployPR_Stage_Template:
    # 'Note: Azure DevOps template does not have an equivalent in GitHub Actions yet'
    env:
      prId: 000
      prUC: PR${{ env.prId }}
      prLC: pr${{ env.prId }}
    if: and(success(),eq(variables['Build.Reason'], 'PullRequest'),ne(variables['System.PullRequest.PullRequestId'], 'Null'))
    steps:
    - uses: actions/checkout@v2
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
            Assert.IsTrue(gitHubOutput.actionsYaml != null);
            Assert.IsTrue(gitHubOutput.actionsYaml != "");
        }

        [TestMethod]
        public void SSDeploymentPipelineTest()
        {
            //Arrange
            Conversion conversion = new Conversion();
            //Source is: https://github.com/samsmithnz/AzurePipelinesToGitHubActionsConverter/issues/128
            string yaml = @"
parameters:
  #Note that pull request environments use Dev credentials
  applicationInsightsApiKey: '$(ApplicationInsights--APIKeyDev)'
  applicationInsightsApplicationId: '$(ApplicationInsights--ApplicationIdDev)'
  applicationInsightsInstrumentationKey: $(ApplicationInsights--InstrumentationKeyDev)
  applicationInsightsLocation: 'East US'
  appServiceContributerClientSecret: $(appServiceContributerClientSecret)
  ASPNETCOREEnvironmentSetting: 'Development'
  captureStartErrors: true
  cognitiveServicesSubscriptionKey: $(cognitiveServicesSubscriptionKey)
  environment: $(prUC)
  environmentLowercase: $(prLC)
  databaseLoginName: $(databaseLoginNameDev) 
  databaseLoginPassword: $(databaseLoginPasswordDev)
  databaseServerName: 'myapp-$(prLC)-eu-sqlserver'
  godaddy_key: $(GoDaddyAPIKey)
  godaddy_secret: $(GoDaddyAPISecret)
  keyVaultClientId: '$(KeyVaultClientId)'
  keyVaultClientSecret: '$(KeyVaultClientSecret)'
  imagesStorageCDNURL: 'https://myapp-$(prLC)-eu-cdnendpoint.azureedge.net/'
  imagesStorageURL: 'https://myapp$(prLC)eustorage.blob.core.windows.net/'
  redisCacheConnectionString: '$(AppSettings--RedisCacheConnectionStringDev)'
  resourceGroupName: 'myapp$(prUC)'
  resourceGroupLocation: 'East US'
  resourceGroupLocationShort: 'eu'
  myappConnectionString: '$(ConnectionStrings--myappConnectionStringDev)'
  serviceName: 'myapp-$(prLC)-eu-service'
  serviceStagingUrl: 'https://myapp-$(prLC)-eu-service-staging.azurewebsites.net/'
  serviceUrl: 'https://myapp-$(prLC)-eu-service.azurewebsites.net/'
  storageAccountName: 'myapp$(prLC)eustorage'
  storageAccountKey: '$(StorageAccountKeyProd)'
  userPrincipalLogin: $(userPrincipalLogin)
  vmImage: $(vmImage)
  websiteName: 'myapp-$(prLC)-eu-web'
  websiteDomainName: '$(prLC).myapp.com'
  websiteStagingUrl: 'https://myapp-$(prLC)-eu-web-staging.azurewebsites.net/'
  websiteUrl: 'https://myapp-$(prLC)-eu-web.azurewebsites.net/'
 
jobs:
  - deployment: DeployFunctionalTests
    displayName: ""Deploy functional tests to ${{parameters.environment}} job""
    environment: ${{parameters.environment}}
    dependsOn: 
    - DeployDatabase
    - DeployWebServiceapp
    - DeployWebsiteapp
    pool:
      vmImage: ${{parameters.vmImage}}        
    strategy:
      runOnce:
        deploy:
          steps:
          - task: DownloadBuildArtifacts@0
            displayName: 'Download the build artifacts'
            inputs:
              buildType: 'current'
              downloadType: 'single'
              artifactName: 'drop'
              downloadPath: '$(build.artifactstagingdirectory)'

";

            //Act
            ConversionResponse gitHubOutput = conversion.ConvertAzurePipelineToGitHubAction(yaml);

            //Assert
            string expected = @"
#Note: Azure DevOps strategy>runOnce>deploy does not have an equivalent in GitHub Actions yetNote: Azure DevOps job environment does not have an equivalent in GitHub Actions yet
env:
  applicationInsightsApiKey: ${{ env.ApplicationInsights--APIKeyDev }}
  applicationInsightsApplicationId: ${{ env.ApplicationInsights--ApplicationIdDev }}
  applicationInsightsInstrumentationKey: ${{ env.ApplicationInsights--InstrumentationKeyDev }}
  applicationInsightsLocation: East US
  appServiceContributerClientSecret: ${{ env.appServiceContributerClientSecret }}
  ASPNETCOREEnvironmentSetting: Development
  captureStartErrors: true
  cognitiveServicesSubscriptionKey: ${{ env.cognitiveServicesSubscriptionKey }}
  environment2: ${{ env.prUC }}
  environment2Lowercase: ${{ env.prLC }}
  databaseLoginName: ${{ env.databaseLoginNameDev }}
  databaseLoginPassword: ${{ env.databaseLoginPasswordDev }}
  databaseServerName: myapp-${{ env.prLC }}-eu-sqlserver
  godaddy_key: ${{ env.GoDaddyAPIKey }}
  godaddy_secret: ${{ env.GoDaddyAPISecret }}
  keyVaultClientId: ${{ env.KeyVaultClientId }}
  keyVaultClientSecret: ${{ env.KeyVaultClientSecret }}
  imagesStorageCDNURL: https://myapp-${{ env.prLC }}-eu-cdnendpoint.azureedge.net/
  imagesStorageURL: https://myapp${{ env.prLC }}eustorage.blob.core.windows.net/
  redisCacheConnectionString: ${{ env.AppSettings--RedisCacheConnectionStringDev }}
  resourceGroupName: myapp${{ env.prUC }}
  resourceGroupLocation: East US
  resourceGroupLocationShort: eu
  myappConnectionString: ${{ env.ConnectionStrings--myappConnectionStringDev }}
  serviceName: myapp-${{ env.prLC }}-eu-service
  serviceStagingUrl: https://myapp-${{ env.prLC }}-eu-service-staging.azurewebsites.net/
  serviceUrl: https://myapp-${{ env.prLC }}-eu-service.azurewebsites.net/
  storageAccountName: myapp${{ env.prLC }}eustorage
  storageAccountKey: ${{ env.StorageAccountKeyProd }}
  userPrincipalLogin: ${{ env.userPrincipalLogin }}
  vmImage: ${{ env.vmImage }}
  websiteName: myapp-${{ env.prLC }}-eu-web
  websiteDomainName: ${{ env.prLC }}.myapp.com
  websiteStagingUrl: https://myapp-${{ env.prLC }}-eu-web-staging.azurewebsites.net/
  websiteUrl: https://myapp-${{ env.prLC }}-eu-web.azurewebsites.net/
jobs:
  DeployFunctionalTests:
    # 'Note: Azure DevOps strategy>runOnce>deploy does not have an equivalent in GitHub Actions yetNote: Azure DevOps job environment does not have an equivalent in GitHub Actions yet'
    name: Deploy functional tests to ${{ env.environment }} job
    runs-on: ${{ env.vmImage }}
    needs:
    - DeployDatabase
    - DeployWebServiceapp
    - DeployWebsiteapp
    steps:
    - name: Download the build artifacts
      uses: actions/download-artifact@v1.0.0
      with:
        name: drop
";

            expected = UtilityTests.TrimNewLines(expected);
            Assert.AreEqual(expected, gitHubOutput.actionsYaml);
            Assert.IsTrue(gitHubOutput.actionsYaml != null);
            Assert.IsTrue(gitHubOutput.actionsYaml != "");
        }

    }
}