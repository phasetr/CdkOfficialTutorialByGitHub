using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeStarConnections;
using Amazon.CDK.Pipelines;
using Constructs;

namespace Workshop;

public class WorkshopPipelineStack : Stack
{
    public WorkshopPipelineStack(Construct parent, string id, IStackProps props = null) : base(parent, id, props)
    {
        // GitHub接続情報：CodeCommit作成コードは消す必要あり
        const string owner = "phasetr";
        const string repository = "CdkOfficialTutorialByGitHub";
        const string branch = "main";
        // CodeStarConnectionsからARNを取得する
        var codeStarConnection = new CfnConnection(this, "WorkshopGitHubCodeStarConnection", new CfnConnectionProps
        {
            ConnectionName = "cdk-workshop-github-connection",
            ProviderType = "GitHub"
        });
        var connectionArn = codeStarConnection.AttrConnectionArn;

        var pipeline = new CodePipeline(this, "Pipeline", new CodePipelineProps
        {
            CodeBuildDefaults = new CodeBuildOptions
            {
                BuildEnvironment = new BuildEnvironment
                {
                    BuildImage = LinuxBuildImage.STANDARD_7_0
                }
            },
            PipelineName = "WorkshopPipeline",
            Synth = new ShellStep("Synth", new ShellStepProps
            {
                // GitHub向けに修正
                // Input = CodePipelineSource.CodeCommit(repo, "main"),
                Input = CodePipelineSource.Connection($"{owner}/{repository}", branch, new ConnectionSourceOptions
                {
                    ConnectionArn = connectionArn
                }),
                Commands = new[]
                {
                    "npm install -g aws-cdk",
                    "wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb",
                    "sudo dpkg -i packages-microsoft-prod.deb",
                    "sudo apt install apt-transport-https",
                    "sudo apt update",
                    "sudo apt-get install -y dotnet-sdk-6.0",
                    "cd src/Workshop && dotnet build",
                    "cd ../../",
                    "npx cdk synth"
                }
            })
        });
        var deploy = new PipelineStage(this, "Deploy");
        var deployStage = pipeline.AddStage(deploy);

        deployStage.AddPost(new ShellStep("TestViewerEndpoint", new ShellStepProps
        {
            EnvFromCfnOutputs = new Dictionary<string, CfnOutput>
            {
                {"ENDPOINT_URL", deploy.HCViewerUrl}
            },
            Commands = new[] {"curl -Ssf $ENDPOINT_URL"}
        }));
        deployStage.AddPost(new ShellStep("TestAPIGatewayEndpoint", new ShellStepProps
        {
            EnvFromCfnOutputs = new Dictionary<string, CfnOutput>
            {
                {"ENDPOINT_URL", deploy.HCEndpoint}
            },
            Commands = new[]
            {
                "curl -Ssf $ENDPOINT_URL/",
                "curl -Ssf $ENDPOINT_URL/hello",
                "curl -Ssf $ENDPOINT_URL/test"
            }
        }));
    }
}
