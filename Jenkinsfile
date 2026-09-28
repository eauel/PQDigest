import hudson.model.Result
import jenkins.model.CauseOfInterruption
import org.jenkinsci.plugins.workflow.steps.FlowInterruptedException

def haltBuildWithSuccess() {
    currentBuild.rawBuild.@result = Result.SUCCESS
    def cause = new CauseOfInterruption.UserInterruption("Build halted programmatically with SUCCESS status")
    throw new FlowInterruptedException(Result.SUCCESS, false, cause)
}

pipeline {
    agent any

    environment {
        github_pat = credentials('github-pat')
        devBranch = "development"
        mainBranch = "master"
        NUGET_PACKAGES = "D:\\NuGetCache"
    }

    stages {
        stage('Prepare Environment') {
            steps {
                script {
                    // Set current Version
                    def fileContent = powershell(returnStdout: true, script:  '''
                        Get-Content -Path "./Scripts/PQDigest.version" -Raw
                    ''').trim()
                    env.pqDigestVersion = fileContent
                    println("PQDigest version: ${env.pqDigestVersion}")
                }
                script {
                    //Set current Commit
                    env.GIT_COMMIT = bat(script: '@git rev-parse HEAD', returnStdout: true).trim()
                    println("Current Git Commit: ${env.GIT_COMMIT}")
                }
                script {
                    //Get last release from git tags
                    bat( script: "@git fetch origin ${env.mainBranch}:refs/remotes/origin/${env.mainBranch}")
                    def mainCommit = bat(script: "@git rev-parse origin/${env.mainBranch}", returnStdout: true).trim()

                    try {
                        env.LAST_RELEASE_TAG = bat(script: "@git describe --tags --abbrev=0 ${mainCommit}", returnStdout: true).trim()
                    }
                    catch (Exception ex) {
                        println("No tags found, setting LAST_RELEASE_TAG to v3.0.0")
                        env.LAST_RELEASE_TAG = "v3.0.0"
                    }
                    println("Last Release Tag: ${env.LAST_RELEASE_TAG}")
                }
            }
        }

        stage('Check Conditions') {
            when {
                not {
                    anyOf {
                        expression { env.BRANCH_NAME == "${env.mainBranch}" }
                        allOf {
                            expression { env.BRANCH_NAME.startsWith("PR") }
                            anyOf {
                                expression { env.CHANGE_TARGET == "${env.devBranch}" }
                                allOf {
                                    expression { env.CHANGE_BRANCH == "${env.devBranch}" }
                                    expression { env.CHANGE_TARGET == "${env.mainBranch}" }
                                }
                            }
                        }
                    }
                }
            }
            steps {
                haltBuildWithSuccess()
            }
        }

        stage('Checkout Master Branch') {
            when {
                expression {
                    return env.BRANCH_NAME == "${env.mainBranch}"
                }
            }
            steps {
                script {
                    bat(script: "@git fetch origin ${env.BRANCH_NAME}:refs/remotes/origin/${env.BRANCH_NAME}")
                    bat(script: "@git checkout origin/${env.BRANCH_NAME}")
                }
            }
        }

        stage('Checkout Development Branch') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}"
                }
            }
            steps {
                script {
                    bat(script: "@git fetch origin ${env.CHANGE_BRANCH}:refs/remotes/origin/${env.CHANGE_BRANCH}")
                    bat(script: "@git checkout origin/${env.CHANGE_BRANCH}")
                }
            }
        }

        stage('Checkout Submodules') {
            steps {
                bat(script: '@git submodule sync --recursive')
                bat(script: '@git submodule update --init --recursive')
                bat(script: '@git submodule status --recursive')
            }
        }

        stage('Application Version') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                }
            }
            steps {
                script {
                    env.GIT_COMMIT = bat(script: '@git rev-parse HEAD', returnStdout: true).trim()
                }
                powershell "powershell.exe -File .\\Scripts\\Versioning.ps1 -VersionFile './Scripts/PQDigest.version' -Commit false"
                powershell "powershell.exe -File .\\Scripts\\PackageVersioning.ps1 -VersionFile './PQDigest/package.json'"
                bat(script: "@git add Scripts/PQDigest.version")
                bat(script: "@git add PQDigest/package.json")
                bat(script: "git diff --cached --quiet || git commit -m \"Updated Version Number\"")
            }
        }

        stage('Gemstone Updates') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                }
            }
            steps {
                powershell "powershell.exe -File .\\Scripts\\GemstoneUpdates.ps1 -VersionFile './Directory.Build.props'"
                powershell "powershell.exe -File .\\Scripts\\CreateDependencyPR.ps1 -GithubToken '${github_pat}' -DevelopmentBranchName '${devBranch}'"
                script {
                    bat(script: "@git add Directory.Build.props")
                    // Avoid an empty commit, which would return a failure code when the pointer is current.
                    bat(script: "git diff --cached --quiet || git commit -m \"Updated Dependencies\"")
                }
            }
        }

        stage('EventWidgets Pointer Update') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                }
            }
            steps {
                dir('PQDigest/EventWidgets') {
                    bat(script: '@git fetch origin main')
                    bat(script: '@git checkout origin/main')
                }
                script {
                    bat(script: '@git add PQDigest/EventWidgets')
                    // Avoid an empty commit, which would return a failure code when the pointer is current.
                    bat(script: 'git diff --cached --quiet || git commit -m "Updated EventWidgets Pointer"')
                }
            }
        }

        stage('Push Changes') {
            when {
                allOf {
                    expression {
                        return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                    }
                    expression {
                        return bat(script: '@git rev-parse HEAD', returnStdout: true).trim() != env.GIT_COMMIT
                    }
                }
            }
            steps {
                powershell "git push origin HEAD:${env.devBranch}"
                haltBuildWithSuccess()
            }
        }

        stage('Build') {
            steps {
                dir('PQDigest') {
                    bat(script: 'npm run build')
                }
                bat(script: 'dotnet build PQDigest/PQDigest.csproj --configuration Release')
            }
        }

        stage('Build Docker Images') {
            when {
                anyOf {
                    expression {
                        return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                    }
                    expression {
                        return env.BRANCH_NAME == "${env.mainBranch}"
                    }
                }
            }
            steps {
                script {
                    env.pqDigestDockerTag = env.CHANGE_BRANCH == "${env.devBranch}" ? "${env.pqDigestVersion}a" : env.pqDigestVersion
                    println("Building PQDigest Docker image tag: pqdigest:${env.pqDigestDockerTag}")
                }

                powershell """
                    dotnet publish '.\\PQDigest\\PQDigest.csproj' `
                        --configuration Release `
                        '-p:PublishProfile=Docker Release Profile PQDigest'
                """

                withCredentials([
                    string(credentialsId: 'wsl-docker-user', variable: 'WSL_DOCKER_USER')
                ]) {
                    powershell '''
                        $key = 'C:\\ProgramData\\Jenkins\\.ssh\\wsl_docker'
                        $wslPath = '/mnt/' + $env:WORKSPACE.Substring(0, 1).ToLowerInvariant() + $env:WORKSPACE.Substring(2).Replace('\\', '/')

                        ssh -i $key `
                            -o BatchMode=yes `
                            -p 2222 `
                            "$env:WSL_DOCKER_USER@localhost" `
                            "cd '$wslPath' && docker info --format '{{.OSType}}' && docker build --build-arg CONFIGURATION=Release -f ./PQDigest.dockerfile -t 'pqdigest:$env:pqDigestDockerTag' ."

                        if ($LASTEXITCODE -ne 0) {
                            exit $LASTEXITCODE
                        }
                    '''
                }
            }
        }

        stage('Publish Application') {
            when {
                expression {
                    return env.BRANCH_NAME == "${env.mainBranch}" ||
                        (env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}")
                }
            }
            steps {
                powershell """
                    dotnet publish '.\\PQDigest\\PQDigest.csproj' `
                        '-p:PublishProfile=Release Profile PQDigest'
                """
            }
        }

        stage('Package Application') {
            when {
                expression {
                    return env.BRANCH_NAME == "${env.mainBranch}" ||
                        (env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}")
                }
            }
            steps {
                script {
                    if (!env.WEBHOST_DELIVERY_DIRECTORY?.trim()) {
                        error('WEBHOST_DELIVERY_DIRECTORY is not configured in Jenkins.')
                    }

                    env.archiveName = env.BRANCH_NAME == "${env.mainBranch}" ?
                        "PQDigest_v${env.pqDigestVersion}.zip" :
                        "PQDigest_v${env.pqDigestVersion}a.zip"
                }
                powershell """
                    [xml]\$publishProfile = Get-Content `
                        -LiteralPath '.\\PQDigest\\Properties\\PublishProfiles\\Release Profile PQDigest.pubxml' `
                        -Raw
                    \$publishDir = (\$publishProfile.Project.PropertyGroup |
                        Where-Object { \$_.PublishDir }).PublishDir

                    Compress-Archive `
                        -Path (Join-Path '.\\PQDigest' "\$publishDir*") `
                        -DestinationPath '${WORKSPACE}\\${env.archiveName}' `
                        -Force
                """
            }
        }

        stage('Comment Prerelease') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                }
            }
            steps {
                powershell """
                    powershell.exe -File .\\Scripts\\GithubComment.ps1 `
                        -Comment 'Prerelease PQDigest v${env.pqDigestVersion}a is available.' `
                        -BranchName '${env.devBranch}' `
                        -GithubToken '${github_pat}' `
                        -RepoOwner 'GridProtectionAlliance' `
                        -RepoName 'PQDigest'
                """
            }
        }

        stage('Deploy Prerelease') {
            when {
                expression {
                    return env.CHANGE_BRANCH == "${env.devBranch}" && env.CHANGE_TARGET == "${env.mainBranch}"
                }
            }
            steps {
                powershell "Move-Item -Path '${WORKSPACE}\\${env.archiveName}' -Destination '${env.WEBHOST_DELIVERY_DIRECTORY}\\PQDigest\\PreRelease\\${env.archiveName}' -Force"
            }
        }

        stage('Deploy Release') {
            when {
                allOf {
                    expression {
                        return env.BRANCH_NAME == "${env.mainBranch}"
                    }
                    expression {
                        return "v${env.pqDigestVersion}" != env.LAST_RELEASE_TAG
                    }
                }
            }
            steps {
                powershell "Move-Item -Path '${WORKSPACE}\\${env.archiveName}' -Destination '${env.WEBHOST_DELIVERY_DIRECTORY}\\PQDigest\\${env.archiveName}' -Force"
                powershell "git tag -a v${env.pqDigestVersion} -m 'Version ${env.pqDigestVersion} release'"
                powershell "git push origin --tags"
            }
        }
    }
}
