using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Configuration;
using System.Dynamic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Reuters.Repositories
{
    public class ReutersService
    {
        private static readonly object _lock = new();
        private static string _accessToken = null;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ReutersService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
        }
        private async Task EnsureAuthToken()
        {
            lock (_lock)
            {
                if (_accessToken != null && DateTime.UtcNow < _tokenExpiry)
                {
                    return;
                }
            }

            await RefreshAuthToken();
        }
        private async Task RefreshAuthToken()
        {
            try
            {

                dynamic requestBody = new ExpandoObject();
                requestBody.client_id = _configuration["api:client_id"];
                requestBody.client_secret = _configuration["api:client_secret"];
                requestBody.grant_type = _configuration["api:grant_type"];
                requestBody.audience = _configuration["api:audience"];
                requestBody.scope = _configuration["api:scope"];
                string jsonBody = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(_configuration["api:authUrl"] + "oauth/token", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();

                    lock (_lock)
                    {
                        _accessToken = responseJson.GetProperty("access_token").GetString();
                        var expiresInSeconds = responseJson.GetProperty("expires_in").GetInt32();
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60);
                    }
                }
                else
                {
                    throw new Exception("Failed to get auth token");
                }
            }
            catch (Exception)
            {

                throw;
            }
        }
        public async Task<string> GetCategories()
        {
            try
            {
                await EnsureAuthToken();
                var request = new HttpRequestMessage(HttpMethod.Post, _configuration["api:apiUrl"] + "content/graphql");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                var query = @"
                        query {
                          filterOptions {
                            categories {
                              code
                              literal
                              uri
                              ... on NamedGroupQuery {
                                children {
                                  code
                                  literal
                                  uri
                                }
                              }
                            }
                          }
                        }";

                var json = JsonSerializer.Serialize(new { query, variables = new { } });
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<string> GetItems(string category, string subCategory, string dateRange, int pageSize = 20, string cursor = null)
        {
            try
            {
                await EnsureAuthToken();
                var request = new HttpRequestMessage(HttpMethod.Post, _configuration["api:apiUrl"] + "content/graphql");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                var filterParts = new List<string>();

                if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(subCategory))
                {
                    filterParts.Add($"namedQueries: {{ filters: \"cat://{category}/{subCategory}\" }}");
                }
                else if (!string.IsNullOrEmpty(category))
                {
                    filterParts.Add($"namedQueries: {{ filters: \"cat://{category}\" }}");
                }

                if (!string.IsNullOrEmpty(dateRange))
                {
                    filterParts.Add($"dateRange: \"{dateRange}\"");
                }

                string filterPart = filterParts.Count > 0
                    ? $"filter: {{ {string.Join(", ", filterParts)} }} sort: {{ direction: ASC, field: VERSION_CREATED }}"
                    : null;
                string cursorPart = string.IsNullOrEmpty(cursor) ? null : $"cursor: \"{cursor}\"";

                var args = new List<string>();
                if (!string.IsNullOrEmpty(cursorPart)) args.Add(cursorPart);
                if (!string.IsNullOrEmpty(filterPart)) args.Add(filterPart);
                args.Add($"limit: {pageSize}");

                string argString = string.Join(", ", args);
                string query = $@"
                            query MyQuery {{
                              search({argString}) {{
                                totalHits
                                items {{
                                  headLine
                                  versionedGuid
                                  uri
                                  language
                                  type
                                  profile
                                  slug
                                  version
                                  credit
                                  firstCreated
                                  sortTimestamp
                                  contentTimestamp
                                  productLabel
                                  urgency
                                }}
                                pageInfo {{
                                  endCursor
                                  hasNextPage
                                }}
                              }}
                            }}";

                var json = JsonSerializer.Serialize(new { query, variables = new { } });
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            catch (Exception)
            {

                throw;
            }

        }
        public async Task<string> GetDescription(string id)
        {
            try
            {
                await EnsureAuthToken();
                var request = new HttpRequestMessage(HttpMethod.Post, _configuration["api:apiUrl"] + "content/graphql");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                string query = @"
                                query MyQuery($id: ID!) {
                                  item(
                                    id: $id
                                    option: {
                                      completeSentences: true, 
                                      fragmentLength: 400, 
                                      previewMode: DIRECT, 
                                      dateFilterField: VERSION_CREATED
                                    }
                                  ) {
                                    byLine
                                    copyrightNotice
                                    versionCreated
                                    fragment
                                    headLine
                                    versionedGuid
                                    uri
                                    language
                                    type
                                    profile
                                    slug
                                    usageTerms
                                    usageTermsRole
                                    version
                                    credit
                                    firstCreated
                                    productLabel
                                    pubStatus
                                    urgency
                                    usn
                                    intro
                                    caption
                                    keyword
                                    channels
                                    subjectLocation { city countryCode countryName }
                                    contributor { code literal role }
                                    renditions {
                                      mimeType
                                      uri
                                      type
                                      version
                                      code
                                    }
                                    associations {
                                      headLine
                                      type
                                      renditions { mimeType uri type version code }
                                    }
                                  }
                                }";

                var variables = new
                {
                    id = id
                };

                var json = JsonSerializer.Serialize(new { query, variables });
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<string> GetNewsDescription(string id)
        {
            try
            {
                await EnsureAuthToken();
                var request = new HttpRequestMessage(HttpMethod.Post, _configuration["api:apiUrl"] + "content/graphql");
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
                string query = @"
                                query GetItemDetails($id: ID!) {
                                 item(
                                    id: $id
                                    option: {
                                      completeSentences: true, 
                                      fragmentLength: 400, 
                                      previewMode: DIRECT, 
                                      dateFilterField: VERSION_CREATED
                                    })
                                  { 
                                    versionedGuid
                                    headLine
                                    fragment
                                    bodyXhtmlRich
                                    firstCreated
                                    sortTimestamp
                                    contentTimestamp
                                  }
                                }";

                var variables = new
                {
                    id = id
                };

                var json = JsonSerializer.Serialize(new { query, variables });
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}