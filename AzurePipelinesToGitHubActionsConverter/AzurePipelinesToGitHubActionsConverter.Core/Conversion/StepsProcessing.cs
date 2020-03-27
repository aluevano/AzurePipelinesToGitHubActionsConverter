﻿using AzurePipelinesToGitHubActionsConverter.Core.Conversion.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AzurePipelinesToGitHubActionsConverter.Core.Conversion
{
    public class StepsProcessing
    {
        //TODO: Add more task types
        public GitHubActions.Step ProcessStep(AzurePipelines.Step step)
        {
            GitHubActions.Step gitHubStep = null;
            if (step.task != null)
            {
                step = CleanStepInputs(step);
                switch (step.task)
                {
                    case "Ant@1":
                        gitHubStep = CreateAntStep(step);
                        break;
                    case "ArchiveFiles@2":
                        gitHubStep = CreateArchiveFilesStep(step);
                        break;
                    case "AzureAppServiceManage@0":
                        gitHubStep = CreateAzureAppServiceManageStep(step);
                        break;
                    case "AzureResourceGroupDeployment@2":
                        gitHubStep = CreateAzureManageResourcesStep(step);
                        break;
                    case "AzureRmWebAppDeployment@3":
                        gitHubStep = CreateAzureWebAppDeploymentStep(step);
                        break;
                    case "CmdLine@2":
                        gitHubStep = CreateScriptStep("cmd", step);
                        break;
                    case "CopyFiles@2":
                        gitHubStep = CreateCopyFilesStep(step);
                        break;
                    case "Docker@2":
                        gitHubStep = CreateDockerStep(step);
                        break;
                    case "DotNetCoreCLI@2":
                        gitHubStep = CreateDotNetCommandStep(step);
                        break;
                    case "DownloadBuildArtifacts@0":
                        gitHubStep = CreateDownloadBuildArtifacts(step);
                        break;
                    case "NuGetCommand@2":
                        gitHubStep = CreateNuGetCommandStep(step);
                        break;
                    case "NuGetToolInstaller@1":
                        gitHubStep = CreateNuGetToolInstallerStep(step);
                        break;
                    case "PowerShell@2":
                        gitHubStep = CreateScriptStep("powershell", step);
                        break;
                    case "PublishPipelineArtifact@0":
                    case "PublishBuildArtifacts@1":
                        gitHubStep = CreatePublishBuildArtifactsStep(step);
                        break;
                    case "PythonScript@0":
                        gitHubStep = CreatePythonStep(step);
                        break;
                    case "SqlAzureDacpacDeployment@1":
                        gitHubStep = CreateSQLAzureDacPacDeployStep(step);
                        break;
                    case "UseDotNet@2":
                        gitHubStep = CreateUseDotNetStep(step);
                        break;
                    case "UsePythonVersion@0":
                        gitHubStep = CreateUsePythonStep(step);
                        break;
                    case "VSBuild@1":
                        gitHubStep = CreateMSBuildStep(step);
                        break;
                    case "VSTest@2":
                        gitHubStep = CreateSeleniumTestingStep(step);
                        break;

                    default:
                        gitHubStep = CreateScriptStep("powershell", step);
                        string newYaml = GenericObjectSerialization.SerializeYaml<AzurePipelines.Step>(step);
                        string[] newYamlSplit = newYaml.Split(Environment.NewLine);
                        StringBuilder yamlBuilder = new StringBuilder();
                        for (int i = 0; i < newYamlSplit.Length; i++)
                        {
                            string line = newYamlSplit[i];
                            if (line.Trim().Length > 0)
                            {
                                yamlBuilder.Append("#");
                                yamlBuilder.Append(line);
                                //if (i < newYamlSplit.Length - 1)
                                //{
                                //    yamlBuilder.Append(Environment.NewLine);
                                //}
                            }
                        }
                        gitHubStep.step_message = "NOTE: This step does not have a conversion path yet: " + step.task;
                        gitHubStep.run = "Write-Host " + gitHubStep.step_message + " " + yamlBuilder.ToString();
                        break;
                }
            }
            else if (step.script != null)
            {
                gitHubStep = new GitHubActions.Step
                {
                    run = step.script,
                    with = step.inputs
                };
            }
            else if (step.pwsh != null)
            {
                gitHubStep = CreateScriptStep("pwsh", step);
            }
            else if (step.powershell != null)
            {
                gitHubStep = CreateScriptStep("powershell", step);
            }
            else if (step.bash != null)
            {
                gitHubStep = CreateScriptStep("bash", step);
            }
            else if (step.publish != null)
            {
                //The shortcut to the build publish step
                //https://docs.microsoft.com/en-us/azure/devops/pipelines/yaml-schema?view=azure-devops&tabs=schema#publish
                gitHubStep = CreatePublishBuildArtifactsStep(step);
            }

            if (gitHubStep != null)
            {
                //Add in generic name and conditions
                if (step.displayName != null)
                {
                    gitHubStep.name = step.displayName;
                }
                if (step.condition != null)
                {
                    gitHubStep._if = ConditionsProcessing.TranslateConditions(step.condition);
                }
                //Double check the with. Sometimes we start to add a property, but for various reasons, we don't use it, and have to null out the with so it doesn't display an empty node in the final yaml
                if (gitHubStep.with != null)
                {
                    if (gitHubStep.with.Count >= 0)
                    {
                        //Look to see if there is non-null data in the collection
                        bool foundData = false;
                        foreach (KeyValuePair<string, string> item in gitHubStep.with)
                        {
                            //If data was found, break out of the loop, we don't need to look anymore
                            if (item.Value != null)
                            {
                                foundData = true;
                                break;
                            }
                        }
                        //If no data was found, null out the with property
                        if (foundData == false)
                        {
                            gitHubStep.with = null;
                        }
                    }
                }
            }
            return gitHubStep;
        }

        //Convert all of the input keys to lowercase, to make pattern matching easier later
        private AzurePipelines.Step CleanStepInputs(AzurePipelines.Step step)
        {
            Dictionary<string, string> newInputs = new Dictionary<string, string>();
            if (step.inputs != null)
            {
                foreach (KeyValuePair<string, string> item in step.inputs)
                {
                    newInputs.Add(item.Key.ToLower(), item.Value);
                }
                step.inputs = newInputs;
            }

            return step;
        }

        private GitHubActions.Step CreateDotNetCommandStep(AzurePipelines.Step step)
        {

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                run = "dotnet "
            };
            if (step.inputs.ContainsKey("command") == true)
            {
                gitHubStep.run += GetStepInput(step, "command") + " ";
            }
            if (step.inputs.ContainsKey("projects") == true)
            {
                gitHubStep.run += GetStepInput(step, "projects") + " ";
            }
            if (step.inputs.ContainsKey("arguments") == true)
            {
                gitHubStep.run += GetStepInput(step, "arguments") + " ";
            }

            //Remove the new line characters
            gitHubStep.run = gitHubStep.run.Replace("\n", "");

            return gitHubStep;
        }

        private GitHubActions.Step CreateDownloadBuildArtifacts(AzurePipelines.Step step)
        {
            string artifactName = GetStepInput(step, "artifactname");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "actions/download-artifact@v1.0.0",
                with = new Dictionary<string, string>
                {
                    { "name", artifactName }
                }
            };

            //From: 
            //- task: DownloadBuildArtifacts@0
            //  displayName: 'Download the build artifacts'
            //  inputs:
            //    buildType: 'current'
            //    downloadType: 'single'
            //    artifactName: 'drop'
            //    downloadPath: '$(build.artifactstagingdirectory)'

            //To:
            //- name: Download serviceapp artifact
            //  uses: actions/download-artifact@v1.0.0
            //  with:
            //    name: serviceapp
            return gitHubStep;
        }

        private GitHubActions.Step CreateCopyFilesStep(AzurePipelines.Step step)
        {
            //Use PowerShell to copy files
            step.script = "Copy " + GetStepInput(step, "sourcefolder") + "/" + GetStepInput(step, "contents") + " " + GetStepInput(step, "targetfolder");

            GitHubActions.Step gitHubStep = CreateScriptStep("powershell", step);
            return gitHubStep;
        }

        private GitHubActions.Step CreateDockerStep(AzurePipelines.Step step)
        {
            //Use PowerShell to copy files
            step.script = "Copy " + GetStepInput(step, "sourcefolder") + "/" + GetStepInput(step, "contents") + " " + GetStepInput(step, "targetfolder");

            GitHubActions.Step gitHubStep = CreateScriptStep("powershell", step);
            return gitHubStep;


            //From: https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/build/docker?view=azure-devops
            //- task: Docker@2
            //  displayName: Login to ACR
            //  inputs:
            //    command: login
            //    containerRegistry: dockerRegistryServiceConnection1
            //- task: Docker@2
            //  displayName: Build
            //  inputs:
            //    command: build
            //    repository: contosoRepository
            //    tags: tag1
            //    arguments: --secret id=mysecret,src=mysecret.txt
            //- task: Docker@2
            //  displayName: Build and Push
            //  inputs:
            //    command: buildAndPush
            //    repository: someUser/contoso
            //    tags: |
            //      tag1
            //      tag2
            //- task: Docker@2
            //  displayName: Logout of ACR
            //  inputs:
            //    command: logout
            //    containerRegistry: dockerRegistryServiceConnection1

            //To: https://github.com/marketplace/actions/docker-build-push

        }

        private GitHubActions.Step CreateScriptStep(string shellType, AzurePipelines.Step step)
        {
            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                run = step.script,
                shell = shellType
            };

            if (gitHubStep.run == null)
            {
                if (step.powershell != null)
                {
                    gitHubStep.run = step.powershell;
                }
                else if (step.pwsh != null)
                {
                    gitHubStep.run = step.pwsh;
                }
                else if (step.bash != null)
                {
                    gitHubStep.run = step.bash;
                }
                else
                {
                    if (step.inputs != null)
                    {
                        string runValue = GetStepInput(step, "script");
                        gitHubStep.run = runValue;
                    }
                }
            }
            if (gitHubStep.shell == "")
            {
                gitHubStep.shell = null;
            }
            if (step.condition != null)
            {
                gitHubStep._if = ConditionsProcessing.TranslateConditions(step.condition);
            }

            return gitHubStep;
        }

        public GitHubActions.Step CreateCheckoutStep()
        {
            //Add the check out step to get the code
            return new GitHubActions.Step
            {
                uses = "actions/checkout@v1"
            };
        }

        public GitHubActions.Step CreateAzureLoginStep()
        {
            //Goal:
            //- name: Log into Azure
            //  uses: azure/login@v1
            //  with:
            //    creds: ${{ secrets.AZURE_SP }}
            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = "Azure Login",
                uses = "azure/login@v1",
                with = new Dictionary<string, string>
                {
                    {"creds","${{ secrets.AZURE_SP }}" }
                }
            };

            //Add note that "AZURE_SP" secret is required
            gitHubStep.step_message = @"Note that ""AZURE_SP"" secret is required to be setup and added into GitHub Secrets: https://help.github.com/en/actions/automating-your-workflow-with-github-actions/creating-and-using-encrypted-secrets";

            return gitHubStep;
        }

        private GitHubActions.Step CreateUseDotNetStep(AzurePipelines.Step step)
        {
            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "actions/setup-dotnet@v1",
                with = new Dictionary<string, string>
                {
                    {"dotnet-version", GetStepInput(step, "version")}
                }
            };
            //Pipelines
            //- task: UseDotNet@2
            //  displayName: 'Use .NET Core sdk'
            //  inputs:
            //    packageType: sdk
            //    version: 2.2.203
            //    installationPath: $(Agent.ToolsDirectory)/dotnet

            //Actions
            //- uses: actions/setup-dotnet@v1
            //  with:
            //    dotnet-version: '2.2.103' # SDK Version to use.
            return gitHubStep;
        }

        private GitHubActions.Step CreateAzureManageResourcesStep(AzurePipelines.Step step)
        {
            string resourceGroup = GetStepInput(step, "resourcegroupname");
            string armTemplateFile = GetStepInput(step, "csmfile");
            string armTemplateParametersFile = GetStepInput(step, "csmparametersfile");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "Azure/github-actions/arm@master",
                env = new Dictionary<string, string>
                {
                    { "AZURE_RESOURCE_GROUP", resourceGroup},
                    { "AZURE_TEMPLATE_LOCATION", armTemplateFile},
                    { "AZURE_TEMPLATE_PARAM_FILE", armTemplateParametersFile},
                }
            };

            //coming from:
            //- task: AzureResourceGroupDeployment@2
            //  displayName: 'Deploy ARM Template to resource group'
            //  inputs:
            //    azureSubscription: 'connection to Azure Portal'
            //    resourceGroupName: $(ResourceGroupName)
            //    location: '[resourceGroup().location]'
            //    csmFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.json'
            //    csmParametersFile: '$(build.artifactstagingdirectory)/drop/ARMTemplates/azuredeploy.parameters.json'
            //    overrideParameters: '-environment $(AppSettings.Environment) -locationShort $(ArmTemplateResourceGroupLocation)'

            //Going to:
            //https://github.com/Azure/github-actions/tree/master/arm
            //action "Manage Azure Resources" {
            //  uses = "Azure/github-actions/arm@master"
            //  env = {
            //    AZURE_RESOURCE_GROUP = "<Resource Group Name"
            //    AZURE_TEMPLATE_LOCATION = "<URL or Relative path in your repository>"
            //    AZURE_TEMPLATE_PARAM_FILE = "<URL or Relative path in your repository>"
            //  }
            //  needs = ["Azure Login"]
            //}}

            return gitHubStep;
        }

        private GitHubActions.Step CreateAzureWebAppDeploymentStep(AzurePipelines.Step step)
        {
            string webappName = GetStepInput(step, "webappname");
            string package = GetStepInput(step, "package");
            string slotName = GetStepInput(step, "slotname");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "Azure/webapps-deploy@v1",
                with = new Dictionary<string, string>
                {
                    { "app-name", webappName},
                    { "package", package},
                    { "slot-name", slotName},
                }
            };

            //coming from:
            //- task: AzureRmWebAppDeployment@3
            //  displayName: 'Azure App Service Deploy: web service'
            //  inputs:
            //    azureSubscription: 'connection to Azure Portal'
            //    WebAppName: $(WebServiceName)
            //    DeployToSlotFlag: true
            //    ResourceGroupName: $(ResourceGroupName)
            //    SlotName: 'staging'
            //    Package: '$(build.artifactstagingdirectory)/drop/MyProject.Service.zip'
            //    TakeAppOfflineFlag: true
            //    JSONFiles: '**/appsettings.json'

            //Going to:
            //- name: Deploy web service to Azure WebApp
            //  uses: Azure/webapps-deploy@v1
            //  with:
            //    app-name: myproject-service
            //    package: serviceapp
            //    slot-name: staging   

            return gitHubStep;
        }

        private GitHubActions.Step CreateNuGetCommandStep(AzurePipelines.Step step)
        {
            string command = GetStepInput(step, "command");
            if (string.IsNullOrEmpty(command) == false)
            {
                command = "restore";
            }
            string restoresolution = GetStepInput(step, "restoresolution");

            GitHubActions.Step gitHubStep = CreateScriptStep("powershell", step);
            gitHubStep.run = "nuget " + command + " " + restoresolution;

            //          - name: Nuget Push
            //run: nuget push *.nupkg

            //coming from:
            //# NuGet
            //# Restore, pack, or push NuGet packages, or run a NuGet command. Supports NuGet.org and authenticated feeds like Azure Artifacts and MyGet. Uses NuGet.exe and works with .NET Framework apps. For .NET Core and .NET Standard apps, use the .NET Core task.
            //- task: NuGetCommand@2
            //  inputs:
            //    #command: 'restore' # Options: restore, pack, push, custom
            //    #restoreSolution: '**/*.sln' # Required when command == Restore
            //    #feedsToUse: 'select' # Options: select, config
            //    #vstsFeed: # Required when feedsToUse == Select
            //    #includeNuGetOrg: true # Required when feedsToUse == Select
            //    #nugetConfigPath: # Required when feedsToUse == Config
            //    #externalFeedCredentials: # Optional
            //    #noCache: false 
            //    #disableParallelProcessing: false 
            //    restoreDirectory: 
            //    #verbosityRestore: 'Detailed' # Options: quiet, normal, detailed
            //    #packagesToPush: '$(Build.ArtifactStagingDirectory)/**/*.nupkg;!$(Build.ArtifactStagingDirectory)/**/*.symbols.nupkg' # Required when command == Push
            //    #nuGetFeedType: 'internal' # Required when command == Push# Options: internal, external
            //    #publishVstsFeed: # Required when command == Push && NuGetFeedType == Internal
            //    #publishPackageMetadata: true # Optional
            //    #allowPackageConflicts: # Optional
            //    #publishFeedCredentials: # Required when command == Push && NuGetFeedType == External
            //    #verbosityPush: 'Detailed' # Options: quiet, normal, detailed
            //    #packagesToPack: '**/*.csproj' # Required when command == Pack
            //    #configuration: '$(BuildConfiguration)' # Optional
            //    #packDestination: '$(Build.ArtifactStagingDirectory)' # Optional
            //    #versioningScheme: 'off' # Options: off, byPrereleaseNumber, byEnvVar, byBuildNumber
            //    #includeReferencedProjects: false # Optional
            //    #versionEnvVar: # Required when versioningScheme == ByEnvVar
            //    #majorVersion: '1' # Required when versioningScheme == ByPrereleaseNumber
            //    #minorVersion: '0' # Required when versioningScheme == ByPrereleaseNumber
            //    #patchVersion: '0' # Required when versioningScheme == ByPrereleaseNumber
            //    #packTimezone: 'utc' # Required when versioningScheme == ByPrereleaseNumber# Options: utc, local
            //    #includeSymbols: false # Optional
            //    #toolPackage: # Optional
            //    #buildProperties: # Optional
            //    #basePath: # Optional, specify path to nuspec files
            //    #verbosityPack: 'Detailed' # Options: quiet, normal, detailed
            //    #arguments: # Required when command == Custom

            //Going to:


            return gitHubStep;
        }

        //https://github.com/warrenbuckley/Setup-Nuget
        private GitHubActions.Step CreateNuGetToolInstallerStep(AzurePipelines.Step step)
        {
            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "warrenbuckley/Setup-Nuget@v1",
                step_message = "Note: Needs to be installed from marketplace: https://github.com/warrenbuckley/Setup-Nuget"
            };

            //coming from:
            //# NuGet tool installer
            //# Acquires a specific version of NuGet from the internet or the tools cache and adds it to the PATH. Use this task to change the version of NuGet used in the NuGet tasks.
            //- task: NuGetToolInstaller@0
            //  inputs:
            //    #versionSpec: '4.3.0' 
            //    #checkLatest: false # Optional

            //Going to:
            //- name: Setup Nuget.exe
            //  uses: warrenbuckley/Setup-Nuget@v1

            return gitHubStep;
        }

        //https://github.com/Azure/sql-action
        private GitHubActions.Step CreateSQLAzureDacPacDeployStep(AzurePipelines.Step step)
        {
            string serverName = GetStepInput(step, "servername");
            string dacPacFile = GetStepInput(step, "dacpacfile");
            string arguments = GetStepInput(step, "additionalarguments");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "azure/sql-action@v1",
                with = new Dictionary<string, string>
                {
                    { "server-name", serverName},
                    { "connection-string", "${{ secrets.AZURE_SQL_CONNECTION_STRING }}"},
                    { "dacpac-package", dacPacFile},
                    { "arguments", arguments }
                },
                step_message = "Note: Connection string needs to be specified - this is different than Pipelines where the server, database, user, and password were specified separately. It's recommended you use secrets for the connection string."
            };

            //coming from:
            //- task: SqlAzureDacpacDeployment@1
            //  displayName: 'Azure SQL dacpac publish'
            //  inputs:
            //    azureSubscription: 'my connection to Azure Portal'
            //    ServerName: '$(databaseServerName).database.windows.net'
            //    DatabaseName: '$(databaseName)'
            //    SqlUsername: '$(databaseLoginName)'
            //    SqlPassword: '$(databaseLoginPassword)'
            //    DacpacFile: '$(build.artifactstagingdirectory)/drop/MyDatabase.dacpac'
            //    additionalArguments: '/p:BlockOnPossibleDataLoss=true'  

            //Going to:
            //- uses: azure/sql-action@v1
            //  with:
            //    server-name: REPLACE_THIS_WITH_YOUR_SQL_SERVER_NAME
            //    connection-string: ${{ secrets.AZURE_SQL_CONNECTION_STRING }}
            //    dacpac-package: './yourdacpacfile.dacpac'

            return gitHubStep;
        }

        private GitHubActions.Step CreateMSBuildStep(AzurePipelines.Step step)
        {
            string solution = GetStepInput(step, "solution");
            //string package = GetStepInput(step, "package");
            //string slotName = GetStepInput(step, "slotname");

            //Currently set to VS2019
            string msBuildLocation = @"C:\Program Files(x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\";

            string run = " " + Environment.NewLine;
            run += "    #TODO: Fix this uglyness." + Environment.NewLine;
            run += "    $msBuildExe = \"" + msBuildLocation + "msbuild.exe\"" + Environment.NewLine;
            run += "    $targetSolution = \"" + solution + "\"" + Environment.NewLine;

            run += "    #Note that the `\" is an escape character sequence to quote strings, and `& is needed to start the command" + Environment.NewLine;
            run += "    $command = \"`& `\"$msBuildExe`\" `\"$targetSolution`\" " + Environment.NewLine;
            run += "    Write - Host \"$command\"" + Environment.NewLine;
            run += "    Invoke - Expression $command";

            //To PowerShell script
            GitHubActions.Step gitHubStep = CreateScriptStep("powershell", step);
            gitHubStep.run = run;

            //coming from:
            //# Visual Studio build
            //# Build with MSBuild and set the Visual Studio version property
            //- task: VSBuild@1
            //  inputs:
            //    #solution: '**\*.sln' 
            //    #vsVersion: 'latest' # Optional. Options: latest, 16.0, 15.0, 14.0, 12.0, 11.0
            //    #msbuildArgs: # Optional
            //    #platform: # Optional
            //    #configuration: # Optional
            //    #clean: false # Optional
            //    #maximumCpuCount: false # Optional
            //    #restoreNugetPackages: false # Optional
            //    #msbuildArchitecture: 'x86' # Optional. Options: x86, x64
            //    #logProjectEvents: true # Optional
            //    #createLogFile: false # Optional
            //    #logFileVerbosity: 'normal' # Optional. Options: quiet, minimal, normal, detailed, diagnostic

            //Going to:
            //- name: Build DotNET35
            //  run: |
            //     "C:\Program Files(x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe \pathtoyoursolutionorproject

            return gitHubStep;
        }

        private GitHubActions.Step CreateSeleniumTestingStep(AzurePipelines.Step step)
        {
            //From:
            //- task: VSTest@2
            //displayName: 'Run functional smoke tests on website and web service'
            //inputs:
            //  searchFolder: '$(build.artifactstagingdirectory)'
            //  testAssemblyVer2: |
            //    **\MyProject.FunctionalTests\MyProject.FunctionalTests.dll
            //  uiTests: true
            //  runSettingsFile: '$(build.artifactstagingdirectory)/drop/FunctionalTests/MyProject.FunctionalTests/test.runsettings'
            //  overrideTestrunParameters: |
            //   -ServiceUrl "https://$(WebServiceName)-staging.azurewebsites.net/" 
            //   -WebsiteUrl "https://$(WebsiteName)-staging.azurewebsites.net/" 
            //   -TestEnvironment "$(AppSettings.Environment)" 

            //Defined in the github windows runner
            string vsTestConsoleLocation = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\Common7\IDE\Extensions\TestPlatform\";

            string run = " |\n";
            run += "    #TODO: Fix this uglyness.\n";
            run += "    $vsTestConsoleExe = \"" + vsTestConsoleLocation + "vstest.console.exe\"\n";
            run += "    $targetTestDll = \"" + GetStepInput(step, "testassemblyver2") + "\"\n";
            run += "    $testRunSettings = \" / Settings:`\"" + GetStepInput(step, "runsettingsfile") + "`\" \"\n";

            string parametersInput = GetStepInput(step, "overridetestrunparameters");
            if (parametersInput != null)
            {
                //Split it two ways, there are 3 combinations, parameters are on each new line, parameters are all on one line, a combination of both multi and single lines.
                //1. Multiline
                string[] multiLineParameters = parametersInput.Split("\n-");
                StringBuilder parameters = new StringBuilder();
                foreach (string multiLineItem in multiLineParameters)
                {
                    //2. Single line 
                    string[] singleLineParameters = multiLineItem.Split(" -");
                    foreach (string item in singleLineParameters)
                    {
                        string[] items = item.Split(" ");
                        if (items.Length >= 2)
                        {
                            //Sometimes the first item has an extra -, remove this.
                            if (items[0].ToString().StartsWith("-") == true)
                            {
                                items[0] = items[0].TrimStart('-');
                            }
                            //build the new format [var name]=[var value]
                            parameters.Append(items[0]);
                            parameters.Append("=");
                            parameters.Append(items[1]);
                            parameters.Append("  ");
                        }
                    }
                }
                run += "    $parameters = \"-- " + parameters.ToString() + "\"";
            }

            //run += "    $parameters = \"\""; //\"-- TestEnvironment = \"Beta123\"  ServiceUrl = \"https://myproject-service-staging.azurewebsites.net/\" WebsiteUrl=\"https://myproject-web-staging.azurewebsites.net/\" \"" + "\n";
            run += "    #Note that the `\" is an escape character sequence to quote strings, and `& is needed to start the command\n";
            run += "    $command = \"`& `\"$vsTestConsoleExe`\" `\"$targetTestDll`\" $testRunSettings $parameters \"\n";
            run += "    Write - Host \"$command\"\n";
            run += "    Invoke - Expression $command";

            //To PowerShell script
            GitHubActions.Step gitHubStep = CreateScriptStep("powershell", step);
            gitHubStep.run = run;
            //gitHubStep.run = @" |
            //    $vsTestConsoleExe = ""C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\Common7\\IDE\\Extensions\\TestPlatform\\vstest.console.exe""
            //    $targetTestDll = """ + GetStepInput(step, "testassemblyver2") + @"""
            //    $testRunSettings = "" / Settings:`""" + GetStepInput(step, "runsettingsfile") + @"`"" ""
            //    $parameters = ""-- TestEnvironment = ""Beta123""  ServiceUrl = ""https://myproject-service-staging.azurewebsites.net/"" WebsiteUrl=""https://myproject-web-staging.azurewebsites.net/"" ""
            //    #Note that the `"" is an escape character to quote strings, and the `& is needed to start the command
            //    $command = ""`& `""$vsTestConsoleExe`"" `""$targetTestDll`"" $testRunSettings $parameters ""
            //    Write - Host ""$command""
            //    Invoke - Expression $command
            //    ";

            return gitHubStep;
        }

        public GitHubActions.Step CreateSetupJavaStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: Ant@1
            //  inputs:
            //    workingDirectory: ''
            //    buildFile: 'build.xml'
            //    javaHomeOption: 'JDKVersion'
            //    jdkVersionOption: '1.8'
            //    jdkArchitectureOption: 'x64'
            //    publishJUnitResults: true
            //    testResultsFiles: '**/TEST-*.xml'  

            //Going to: //https://github.com/marketplace/actions/create-zip-file
            //- name: Set up JDK 1.8
            //  uses: actions/setup-java@v1
            //  with:
            //    java-version: 1.8 

            //Get the Java version and use it to build the task
            string jdkVersionOption = GetStepInput(step, "jdkVersionOption");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = "Setup JDK " + jdkVersionOption,
                uses = "actions/setup-java@v1",
                with = new Dictionary<string, string>
                {
                    { "java-version", jdkVersionOption}
                }
            };

            return gitHubStep;
        }


        private GitHubActions.Step CreateAntStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: Ant@1
            //  inputs:
            //    workingDirectory: ''
            //    buildFile: 'build.xml'
            //    javaHomeOption: 'JDKVersion'
            //    jdkVersionOption: '1.8'
            //    jdkArchitectureOption: 'x64'
            //    publishJUnitResults: true
            //    testResultsFiles: '**/TEST-*.xml'  

            //Going to:
            //- name: Set up JDK 1.8
            //  uses: actions/setup-java@v1
            //  with:
            //    java-version: 1.8 
            //- name: Build with Ant
            //  run: ant -noinput -buildfile build.xml

            string buildFile = GetStepInput(step, "buildFile");

            string antCommand = "ant -noinput -buildfile " + buildFile;
            step.script = antCommand;

            GitHubActions.Step gitHubStep = CreateScriptStep("", step);

            return gitHubStep;
        }

        private GitHubActions.Step CreateUsePythonStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: UsePythonVersion@0
            //  inputs:
            //    versionSpec: '3.7'
            //    addToPath: true
            //    architecture: 'x64'

            //Going to:
            //- name: Setup Python 3.7
            //  uses: actions/setup-python@v1
            //  with:
            //    python-version: '3.7'

            string version = GetStepInput(step, "versionSpec");

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                name = "Setup Python " + version,
                uses = "actions/setup-python@v1",
                with = new Dictionary<string, string>
                {
                    { "python-version", version}
                }
            };


            return gitHubStep;
        }

        private GitHubActions.Step CreatePythonStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: PythonScript@0
            //  inputs:
            //    scriptSource: 'filePath'
            //    scriptPath: 'Python/Hello.py'

            //Going to:
            //- run: python Python/Hello.py

            string scriptPath = GetStepInput(step, "scriptPath");

            string pythonCommand = "python " + scriptPath;
            step.script = pythonCommand;

            GitHubActions.Step gitHubStep = CreateScriptStep("", step);

            return gitHubStep;
        }

        private GitHubActions.Step CreateArchiveFilesStep(AzurePipelines.Step step)
        {
            //coming from:
            //- task: ArchiveFiles@2
            //  displayName: 'Archive files'
            //  inputs:
            //    rootFolderOrFile: '$(System.DefaultWorkingDirectory)/publish_output'
            //    includeRootFolder: false
            //    archiveType: zip
            //    archiveFile: $(Build.ArtifactStagingDirectory)/$(Build.BuildId).zip
            //    replaceExistingArchive: true

            //Going to: //https://github.com/marketplace/actions/create-zip-file
            //- uses: montudor/action-zip@v0.1.0
            //  with:
            //    args: zip -qq -r ./dir.zip ./dir

            string rootFolderOrFile = GetStepInput(step, "rootFolderOrFile");
            string archiveFile = GetStepInput(step, "archiveFile");

            string zipCommand = "zip -qq -r " + archiveFile + " " + rootFolderOrFile;

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "montudor/action-zip@v0.1.0",
                with = new Dictionary<string, string>
                {
                    { "args", zipCommand}
                },
                step_message = "Note: Needs to be installed from marketplace: https://github.com/marketplace/actions/create-zip-file"
            };

            return gitHubStep;
        }

        private GitHubActions.Step CreateAzureAppServiceManageStep(AzurePipelines.Step step)
        {
            //https://docs.microsoft.com/en-us/azure/devops/pipelines/tasks/deploy/azure-app-service-manage?view=azure-devops
            //coming from:
            //- task: AzureAppServiceManage@0
            //displayName: 'Swap Slots: web service'
            //inputs:
            //  azureSubscription: 'connection to Azure Portal'
            //  WebAppName: $(WebServiceName)
            //  ResourceGroupName: $(ResourceGroupName)
            //  SourceSlot: 'staging'

            //Going to:
            //- name: Swap web service staging slot to production
            //  uses: Azure/cli@v1.0.0
            //  with:
            //    inlineScript: az webapp deployment slot swap --resource-group MyProjectRG --name featureflags-data-eu-service --slot staging --target-slot production


            string resourceGroup = GetStepInput(step, "resourcegroupname");
            string webAppName = GetStepInput(step, "webappname");
            string sourceSlot = GetStepInput(step, "sourceslot");
            string targetSlot = GetStepInput(step, "targetslot");
            if (string.IsNullOrEmpty(targetSlot) == true)
            {
                targetSlot = "production";
            }
            //TODO: Add other properties for az webapp deployment

            string script = "az webapp deployment slot swap --resource-group " + resourceGroup +
                " --name " + webAppName +
                " --slot " + sourceSlot +
                " --target-slot " + targetSlot + "";

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "Azure/cli@v1.0.0",
                with = new Dictionary<string, string>
                {
                    { "inlineScript", script}
                }
            };

            return gitHubStep;
        }

        private GitHubActions.Step CreatePublishBuildArtifactsStep(AzurePipelines.Step step)
        {
            //There are 3 Azure DevOps variations
            //# Publish the artifacts
            //- task: PublishBuildArtifacts@1
            //  displayName: 'Publish Artifact'
            //  inputs:
            //    artifactName: drop
            //    PathtoPublish: '$(build.artifactstagingdirectory)'";

            //# Publishing pipeline artifacts is almost identical
            //- task: PublishPipelineArtifact@0
            //  displayName: Store artifact
            //  inputs:
            //    artifactName: 'MyProject'
            //    targetPath: 'MyProject/bin/release/netcoreapp2.2/publish/'

            //- publish: $(Build.ArtifactStagingDirectory)/$(Build.BuildId).zip
            //  artifact: drop

            //- name: publish build artifacts back to GitHub
            //  uses: actions/upload-artifact@master
            //  with:
            //    name: console exe
            //    path: /home/runner/work/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter/AzurePipelinesToGitHubActionsConverter.ConsoleApp/bin/Release/netcoreapp3.0

            string name = "";
            if (step.inputs != null && step.inputs.ContainsKey("artifactname") == true)
            {
                name = GetStepInput(step, "artifactname");
            }
            string path = "";
            if (step.task == "PublishBuildArtifacts@1")
            {
                path = GetStepInput(step, "pathtopublish");
            }
            else if (step.task == "PublishPipelineArtifact@0")
            {
                path = GetStepInput(step, "targetpath");
            }
            else if (step.publish != null)
            {
                name = step.artifact;
                path = step.publish;
            }

            GitHubActions.Step gitHubStep = new GitHubActions.Step
            {
                uses = "actions/upload-artifact@master",
                with = new Dictionary<string, string>
                {
                    {"path", path}
                }
            };
            if (string.IsNullOrEmpty(name) == false)
            {
                gitHubStep.with.Add("name", name);
            }

            //In publish task, I we need to delete any usage of build.artifactstagingdirectory variable as it's implied in github actions, and therefore not needed (Adding it adds the path twice)
            if (gitHubStep.with.ContainsKey("path") == true && gitHubStep.with["path"] != null)
            {
                gitHubStep.with["path"].Replace("$(build.artifactstagingdirectory)", "");
            }
            return gitHubStep;
        }

        //Safely extract the step input, if it exists
        private string GetStepInput(AzurePipelines.Step step, string name)
        {
            string input = null;
            //Make the name lowercase to help prevent conflicts later
            if (step.inputs != null && name != null)
            {
                name = name.ToLower();
                //Extract the input
                foreach (KeyValuePair<string, string> item in step.inputs)
                {
                    if (item.Key.ToLower() == name)
                    {
                        input = item.Value;
                        break;
                    }
                }
            }
            return input;
        }
    }
}