# OrgRepoWatchmanApp
Solution for GitHub API Challenge

## GitHub API Challenge
GitHub has a powerful API that enables developers to easily access GitHub data. Companies often ask us to craft solutions to their specific problems. A common request we receive is for branches to be automatically protected upon creation.

Please create a simple web service that listens for organization events to know when a repository has been created. 

When the repository is created please automate the protection of the master branch. 

Notify yourself with an @mention in an issue within the repository that outlines the protections that were added.

Some things you will need:

* a GitHub account
* an organization (you can create one for free)
* a repository (in order to get a branch, you need a commit! Make sure to initialize with a README)
* a web service that listens for webhook deliveries
* A README.md file in your web service's repository that documents how to run and use the service. Documentation is highly valued at GitHub and on the Professional Services team.
* Be prepared to demo this solution during your following interview

I believe the restrictions are these https://docs.github.com/en/github/administering-a-repository/enabling-branch-restrictions.

# Solution

## Github Side
Create Github Org https://github.com/DanielOdievichOrg.

Create GitHub App https://docs.github.com/en/developers/apps/creating-a-github-app (mine is https://github.com/organizations/DanielOdievichOrg/settings/apps/orgrepowatchmanapp2).

Grant OrgRepoWatchmanApp2 App permissions to operate within organization. I chose these:

	Administration 
	Repository creation, deletion, settings, teams, and collaborators.
	Contents 
	Repository contents, commits, branches, downloads, releases, and merges.
	Pull requests 
	Pull requests and related comments, assignees, labels, milestones, and merges.
	Projects 
	Manage repository projects, columns, and cards.
	Commit statuses 
	Commit statuses.
	Metadata 
    Search repositories, list collaborators, and access repository metadata.
    Administration 
	Manage access to an organization.
    Projects 
    Manage organization projects, columns, and cards.

Subscribe OrgRepoWatchmanApp2 App to this event:
    
    Repository
    Repository created, deleted, archived, unarchived, publicized, privatized, edited, renamed, or transferred.

Create a Personal Access Token for API usage (https://docs.github.com/en/github/authenticating-to-github/creating-a-personal-access-token).

Point the webhook setting at the Azure web function app and create a secret for hashing messages sent by webhook (https://docs.github.com/en/developers/webhooks-and-events/securing-your-webhooks).

![](docs/GitHubWebHook.png?raw=true)
[Full Size](docs/GitHubWebHook.png?raw=true)

## Azure Side
Create a Function App in Azure. I chose my MSDN subscription to create an instance in West US on a consumption plan called OrgRepoWatchmanWebHook.

Expose it via web address. Mine is at https://orgrepowatchmanwebhook.azurewebsites.net/

Created a C# .NET Core Function called HttpTrigger.

Add following settings to the Function App:

Setting | Purpose
-- | -- 
OrgRepoWatchmanApp2_USERNAME | Username for signing into Github
OrgRepoWatchmanApp2_TOKEN | Personal Access Token for signing into Github
OrgRepoWatchmanApp2_SECRET | Secret used by Github to check message validity via SHA1 hash

![](docs/AzureWebSiteSettings.png?raw=true)
[Full Size](docs/AzureWebSiteSettings.png?raw=true)

Use the code in C# .NET Core application (run.csx) to listen to the message arriving, validate the hash, parse the message type, decide to operate on the right message, adjust branch protection level and create an issue.
