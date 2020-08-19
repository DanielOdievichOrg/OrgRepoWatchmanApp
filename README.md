# OrgRepoWatchmanApp
Solution for GitHub API Challenge

## GitHub API Challenge
GitHub has a powerful API that enables developers to easily access GitHub data. Companies often ask us to craft solutions to their specific problems. A common request we receive is for branches to be automatically protected upon creation.
Please create a simple web service that listens for organization events to know when a repository has been created. When the repository is created please automate the protection of the master branch. Notify yourself with an @mention in an issue within the repository that outlines the protections that were added.
Some things you will need:
	· a GitHub account
	· an organization (you can create one for free)
	· a repository (in order to get a branch, you need a commit! Make sure to initialize with a README)
	· a web service that listens for webhook deliveries
	· A README.md file in your web service's repository that documents how to run and use the service. Documentation is highly valued at GitHub and on the Professional Services team.
	· Be prepared to demo this solution during your following interview

# Solution

## Github Side
Created Github Org https://github.com/DanielOdievichOrg

Created GitHub App https://github.com/organizations/DanielOdievichOrg/settings/apps/orgrepowatchmanapp2

Granted OrgRepoWatchmanApp2 App permissions to the organization.

Subscribed OrgRepoWatchmanApp2 App to "Repository created, deleted, archived, unarchived, publicized, privatized, edited, renamed, or transferred." events.

Created a secret for hashing messages sent by webhook.

Created a Personal Access Token for API usage.

Pointed the webhook setting at the Azure web function app.

## Azure Side
Created a Function App in Azure West US on consumption plan.

Exposed it via https://orgrepowatchmanwebhook.azurewebsites.net/

Created a Function called HttpTrigger.

Added settings:

Setting | Purpose
-- | -- 
OrgRepoWatchmanApp2_USERNAME | Username for signing into Github
OrgRepoWatchmanApp2_TOKEN | Personal Access Token for signing into Github
OrgRepoWatchmanApp2_SECRET | Secret used by Github to check message validity via SHA1 hash

Added C# .NET Core application (run.csx) to listen to the message, validate the hash, parse the message type, and react accordingly.
