#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Cryptography;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

    // Validate the message hash
    string secretKey = Environment.GetEnvironmentVariable("OrgRepoWatchmanApp2_SECRET");
    string computedHash = String.Format("sha1={0}", GetHash(requestBody, secretKey));
    string githubHash = req.Headers["X-Hub-Signature"];
    log.LogInformation(String.Format("Computed Hash='{0}'", computedHash));
    log.LogInformation(String.Format("Received Hash='{0}'", githubHash));
    if (String.Compare(computedHash, githubHash, false) != 0)
    {
        log.LogError("Message hash was not passed or doesn't match calculated value");

        var result = new ObjectResult("Message hash was not passed or doesn't match calculated value");
        result.StatusCode = StatusCodes.Status401Unauthorized ;
        return result;
    }

    // Get message information
    string githubTraceID = req.Headers["X-GitHub-Delivery"];
    string githubEventType = req.Headers["X-GitHub-Event"];

    log.LogInformation(String.Format("Event='{0}', TraceID='{1}'\n{2}", githubEventType, githubTraceID, requestBody));

    // Check for the right kind of event
    // We want this one and no other
    // https://docs.github.com/en/developers/webhooks-and-events/webhook-events-and-payloads#repository
    if (String.Compare(githubEventType, "repository", false) != 0)
    {
        log.LogError(String.Format("Event type '{0}' is not supported by this web hook", githubEventType));

        var result = new ObjectResult(String.Format("Event type '{0}' is not supported by this web hook", githubEventType));
        result.StatusCode = StatusCodes.Status400BadRequest;
        return result;
    }

    // Now we have the right event. Let's do what the exercise is calling for
    dynamic eventObjectDynamic = JsonConvert.DeserializeObject(requestBody);
    if (eventObjectDynamic != null)
    {
        JToken eventToken = (JToken)eventObjectDynamic;

        string eventSubType = eventToken["action"].ToString().ToLower();
        log.LogInformation(String.Format("Event subtype='{0}'", eventSubType));

        switch (eventSubType)
        {
            // Supporting creation of public repo
            case "created":
            // Supporting making private repo public
            case "publicized":
                if (eventSubType == "created")
                {
                    log.LogInformation("Sleeping!");
                    // There seems to be a timing issue with branches not yet being created when repo is created
                    System.Threading.Thread.Sleep(5000);
                }

                JToken repositoryToken = (JToken)eventToken["repository"];
                string repositoryName = repositoryToken["full_name"].ToString();
                log.LogInformation(String.Format("Operating on repository='{0}'", repositoryName));

                // Protection only works on public repos for my accounts
                bool isRepoPrivate = (bool)repositoryToken["private"];
                if (isRepoPrivate == true)
                {
                    log.LogWarning(String.Format("Repository '{0}' is private, unable to change the protection level", repositoryName));

                    var result1 = new ObjectResult(String.Format("Repository '{0}' is private, unable to change the protection level", repositoryName));
                    result1.StatusCode = StatusCodes.Status400BadRequest;
                    return result1;
                }

                log.LogInformation(String.Format("Repository '{0}' is public, will try to change the protection level", repositoryName));

                // Using Personal Access Token
                // https://docs.github.com/en/rest/overview/other-authentication-methods
                string authUsername = Environment.GetEnvironmentVariable("OrgRepoWatchmanApp2_USERNAME");
                string authToken = Environment.GetEnvironmentVariable("OrgRepoWatchmanApp2_TOKEN");
                // log.LogInformation(String.Format("authUsername='{0}' authToken={1}", authUsername, authToken));

                // https://docs.github.com/en/rest/reference/repos#update-branch-protection
                string protectionAPIUrl = String.Format("{0}/branches/main/protection", repositoryToken["url"].ToString());
                string protectionSettingsBody = "{\"required_status_checks\":{\"strict\":true,\"contexts\":[\"contexts\"]},\"enforce_admins\":true,\"required_pull_request_reviews\":{\"dismissal_restrictions\":{\"users\":[\"users\"],\"teams\":[\"teams\"]},\"dismiss_stale_reviews\":true,\"require_code_owner_reviews\":true,\"required_approving_review_count\":3},\"restrictions\":{\"users\":[\"users\"],\"teams\":[\"teams\"],\"apps\":[\"apps\"]}}";

                using (HttpClient httpClient = new HttpClient())
                {
                    log.LogInformation(String.Format("Calling API='{0}' with body=\n{1}", protectionAPIUrl, protectionSettingsBody));

                    // Very interesting value for luke cage, but https://docs.github.com/en/rest/reference/repos#update-branch-protection says it must be so right now
                    // Update on 04/17/2022, the value is now application/vnd.github.v3+json
                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json");
                    httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    AuthenticationHeaderValue authentication = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", authUsername, authToken))));
                    httpClient.DefaultRequestHeaders.Authorization = authentication;
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Daniel Odievich");
                    httpClient.BaseAddress = new Uri(protectionAPIUrl);
                    StringContent content = new StringContent(protectionSettingsBody);

                    HttpResponseMessage responseFromGithub = await httpClient.PutAsync(protectionAPIUrl, content);
                    if (responseFromGithub.IsSuccessStatusCode)
                    {
                        string resultString = responseFromGithub.Content.ReadAsStringAsync().Result;
                        log.LogInformation(resultString);
                    }
                    else
                    {
                        string resultString = responseFromGithub.Content.ReadAsStringAsync().Result;
                        log.LogError("Unable to execute Github API with '{0}' '{1}'\n{2}", responseFromGithub.StatusCode, responseFromGithub.ReasonPhrase, resultString);

                        var result2 = new ObjectResult(String.Format("Could not modify '{0}' repository branch protection", repositoryName));
                        result2.StatusCode = StatusCodes.Status500InternalServerError;
                        return result2;
                    }
                }

                // https://docs.github.com/en/rest/reference/issues#create-an-issue
                string issueAPIUrl = String.Format("{0}/issues", repositoryToken["url"].ToString());
                string issueBody = "{\"title\": \"This Repository main branch was protected\", \"body\": \"This branch was protected via /branches/main/protection API. FYI @danielodievich\"}";
                
                using (HttpClient httpClient = new HttpClient())
                {
                    log.LogInformation(String.Format("Calling API='{0}' with body=\n{1}", issueAPIUrl, issueBody));

                    MediaTypeWithQualityHeaderValue accept = new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json");
                    httpClient.DefaultRequestHeaders.Accept.Add(accept);
                    AuthenticationHeaderValue authentication = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(String.Format("{0}:{1}", authUsername, authToken))));
                    httpClient.DefaultRequestHeaders.Authorization = authentication;
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Daniel Odievich");
                    httpClient.BaseAddress = new Uri(issueAPIUrl);
                    StringContent content = new StringContent(issueBody);

                    HttpResponseMessage responseFromGithub = await httpClient.PostAsync(issueAPIUrl, content);
                    if (responseFromGithub.IsSuccessStatusCode)
                    {
                        string resultString = responseFromGithub.Content.ReadAsStringAsync().Result;
                        log.LogInformation(resultString);
                    }
                    else
                    {
                        string resultString = responseFromGithub.Content.ReadAsStringAsync().Result;
                        log.LogError("Unable to execute Github API with '{0}' '{1}'\n{2}", responseFromGithub.StatusCode, responseFromGithub.ReasonPhrase, resultString);

                        var result3 = new ObjectResult(String.Format("Could not create issue in '{0}' repository", repositoryName));
                        result3.StatusCode = StatusCodes.Status500InternalServerError;
                        return result3;
                    }
                }

                break;

            default:
                log.LogWarning(String.Format("Event type '{0}' with sub type '{1}' is not supported by this web hook", githubEventType, eventSubType));

                var result = new ObjectResult(String.Format("Event type '{0}' with sub type '{1}' is not supported by this web hook", githubEventType, eventSubType));
                result.StatusCode = StatusCodes.Status400BadRequest;
                return result;
                break;

        }
    }

    return new OkObjectResult("All processing completed successfully");
}

public static string GetHash(String text, String key)
{
    UTF8Encoding encoding = new UTF8Encoding();

    Byte[] textBytes = encoding.GetBytes(text);
    Byte[] keyBytes = encoding.GetBytes(key);

    Byte[] hashBytes;

    using (HMACSHA1 hash = new HMACSHA1(keyBytes))
        hashBytes = hash.ComputeHash(textBytes);

    return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
}
