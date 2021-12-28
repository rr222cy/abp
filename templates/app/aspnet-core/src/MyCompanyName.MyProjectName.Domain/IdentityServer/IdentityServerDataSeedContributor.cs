using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4;
using IdentityServer4.Models;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.IdentityServer.ApiResources;
using Volo.Abp.IdentityServer.ApiScopes;
using Volo.Abp.IdentityServer.Clients;
using Volo.Abp.IdentityServer.IdentityResources;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using ApiResource = Volo.Abp.IdentityServer.ApiResources.ApiResource;
using ApiScope = Volo.Abp.IdentityServer.ApiScopes.ApiScope;
using Client = Volo.Abp.IdentityServer.Clients.Client;

namespace MyCompanyName.MyProjectName.IdentityServer;

public class IdentityServerDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IApiResourceRepository _apiResourceRepository;
    private readonly IApiScopeRepository _apiScopeRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IIdentityResourceDataSeeder _identityResourceDataSeeder;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IPermissionDataSeeder _permissionDataSeeder;
    private readonly IConfiguration _configuration;
    private readonly ICurrentTenant _currentTenant;

    public IdentityServerDataSeedContributor(
        IClientRepository clientRepository,
        IApiResourceRepository apiResourceRepository,
        IApiScopeRepository apiScopeRepository,
        IIdentityResourceDataSeeder identityResourceDataSeeder,
        IGuidGenerator guidGenerator,
        IPermissionDataSeeder permissionDataSeeder,
        IConfiguration configuration,
        ICurrentTenant currentTenant)
    {
        _clientRepository = clientRepository;
        _apiResourceRepository = apiResourceRepository;
        _apiScopeRepository = apiScopeRepository;
        _identityResourceDataSeeder = identityResourceDataSeeder;
        _guidGenerator = guidGenerator;
        _permissionDataSeeder = permissionDataSeeder;
        _configuration = configuration;
        _currentTenant = currentTenant;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        using (_currentTenant.Change(context?.TenantId))
        {
            await _identityResourceDataSeeder.CreateStandardResourcesAsync();
            await CreateApiResourcesAsync();
            await CreateApiScopesAsync();
            await CreateClientsAsync();
        }
    }

    private async Task CreateApiScopesAsync()
    {
        await CreateApiScopeAsync("MyProjectName");
    }

    private async Task CreateApiResourcesAsync()
    {
        await CreateApiResourceAsync("MyProjectName");
    }

    private async Task<ApiResource> CreateApiResourceAsync(string name, IEnumerable<string> claims)
    {
        var apiResource = await _apiResourceRepository.FindByNameAsync(name);
        if (apiResource == null)
        {
            apiResource = await _apiResourceRepository.InsertAsync(
                new ApiResource(
                    _guidGenerator.Create(),
                    name,
                    name + " API"
                ),
                autoSave: true
            );
        }

        return await _apiResourceRepository.UpdateAsync(apiResource);
    }

    private async Task<ApiScope> CreateApiScopeAsync(string name)
    {
        var apiScope = await _apiScopeRepository.FindByNameAsync(name);
        if (apiScope == null)
        {
            apiScope = await _apiScopeRepository.InsertAsync(
                new ApiScope(
                    _guidGenerator.Create(),
                    name,
                    name + " API"
                ),
                autoSave: true
            );
        }

        return apiScope;
    }

    private async Task CreateClientsAsync()
    {
        var commonScopes = new[]
        {
            IdentityServerConstants.StandardScopes.OpenId,
            IdentityServerConstants.StandardScopes.Email,
            IdentityServerConstants.StandardScopes.Profile,
            IdentityServerConstants.StandardScopes.Phone,
            IdentityServerConstants.StandardScopes.Address,
            "role",
            "Tenant",
            "MyProjectName"
        };
        var configurationSection = _configuration.GetSection("IdentityServer:Clients");

        //<TEMPLATE-REMOVE IF-NOT='ui:mvc&&tiered'>

        //Web Client
        var webClientId = configurationSection["MyProjectName_Web:ClientId"];
        if (!webClientId.IsNullOrWhiteSpace())
        {
            var webClientRootUrl = configurationSection["MyProjectName_Web:RootUrl"].EnsureEndsWith('/');

            await CreateClientAsync(
                name: webClientId,
                scopes: commonScopes,
                grantTypes: new[] { "hybrid" },
                secret: (configurationSection["MyProjectName_Web:ClientSecret"] ?? "1q2w3e*").Sha256(),
                redirectUri: $"{webClientRootUrl}signin-oidc",
                postLogoutRedirectUri: $"{webClientRootUrl}signout-callback-oidc",
                frontChannelLogoutUri: $"{webClientRootUrl}Account/FrontChannelLogout",
                corsOrigins: new[] { webClientRootUrl.RemovePostFix("/") }
            );
        }

        //</TEMPLATE-REMOVE>

        //Console Test / Angular Client
        var consoleAndAngularClientId = configurationSection["MyProjectName_App:ClientId"];
        if (!consoleAndAngularClientId.IsNullOrWhiteSpace())
        {
            var webClientRootUrl = configurationSection["MyProjectName_App:RootUrl"]?.TrimEnd('/');

            await CreateClientAsync(
                name: consoleAndAngularClientId,
                scopes: commonScopes,
                grantTypes: new[] { "password", "client_credentials", "authorization_code" },
                secret: (configurationSection["MyProjectName_App:ClientSecret"] ?? "1q2w3e*").Sha256(),
                requireClientSecret: false,
                redirectUri: webClientRootUrl,
                postLogoutRedirectUri: webClientRootUrl,
                corsOrigins: new[] { webClientRootUrl.RemovePostFix("/") }
            );
        }

        //<TEMPLATE-REMOVE IF-NOT='ui:blazor'>

        // Blazor Client
        var blazorClientId = configurationSection["MyProjectName_Blazor:ClientId"];
        if (!blazorClientId.IsNullOrWhiteSpace())
        {
            var blazorRootUrl = configurationSection["MyProjectName_Blazor:RootUrl"].TrimEnd('/');

            await CreateClientAsync(
                name: blazorClientId,
                scopes: commonScopes,
                grantTypes: new[] { "authorization_code" },
                secret: configurationSection["MyProjectName_Blazor:ClientSecret"]?.Sha256(),
                requireClientSecret: false,
                redirectUri: $"{blazorRootUrl}/authentication/login-callback",
                postLogoutRedirectUri: $"{blazorRootUrl}/authentication/logout-callback",
                corsOrigins: new[] { blazorRootUrl.RemovePostFix("/") }
            );
        }

        //</TEMPLATE-REMOVE>

        //<TEMPLATE-REMOVE IF-NOT='ui:blazor-server&&tiered'>

        //Blazor Server Tiered Client
        var blazorServerTieredClientId = configurationSection["MyProjectName_BlazorServerTiered:ClientId"];
        if (!blazorServerTieredClientId.IsNullOrWhiteSpace())
        {
            var blazorServerTieredClientRootUrl = configurationSection["MyProjectName_BlazorServerTiered:RootUrl"].EnsureEndsWith('/');

            /* MyProjectName_BlazorServerTiered client is only needed if you created a tiered blazor server
             * solution. Otherwise, you can delete this client. */

            await CreateClientAsync(
                name: blazorServerTieredClientId,
                scopes: commonScopes,
                grantTypes: new[] { "hybrid" },
                secret: (configurationSection["MyProjectName_BlazorServerTiered:ClientSecret"] ?? "1q2w3e*").Sha256(),
                redirectUri: $"{blazorServerTieredClientRootUrl}signin-oidc",
                postLogoutRedirectUri: $"{blazorServerTieredClientRootUrl}signout-callback-oidc",
                frontChannelLogoutUri: $"{blazorServerTieredClientRootUrl}Account/FrontChannelLogout",
                corsOrigins: new[] { blazorServerTieredClientRootUrl.RemovePostFix("/") }
            );
        }

        //</TEMPLATE-REMOVE>

        // Swagger Client
        var swaggerClientId = configurationSection["MyProjectName_Swagger:ClientId"];
        if (!swaggerClientId.IsNullOrWhiteSpace())
        {
            var swaggerRootUrl = configurationSection["MyProjectName_Swagger:RootUrl"].TrimEnd('/');

            await CreateClientAsync(
                name: swaggerClientId,
                scopes: commonScopes,
                grantTypes: new[] { "authorization_code" },
                secret: configurationSection["MyProjectName_Swagger:ClientSecret"]?.Sha256(),
                requireClientSecret: false,
                redirectUri: $"{swaggerRootUrl}/swagger/oauth2-redirect.html",
                corsOrigins: new[] { swaggerRootUrl.RemovePostFix("/") }
            );
        }
    }

    private async Task<Client> CreateClientAsync(
        string name,
        IEnumerable<string> scopes,
        IEnumerable<string> grantTypes,
        string secret = null,
        string redirectUri = null,
        string postLogoutRedirectUri = null,
        string frontChannelLogoutUri = null,
        bool requireClientSecret = true,
        bool requirePkce = false,
        IEnumerable<string> permissions = null,
        IEnumerable<string> corsOrigins = null)
    {
        var client = await _clientRepository.FindByClientIdAsync(name);
        if (client == null)
        {
            client = await _clientRepository.InsertAsync(
                new Client(
                    _guidGenerator.Create(),
                    name
                )
                {
                    ClientName = name,
                    ProtocolType = "oidc",
                    Description = name,
                    AlwaysIncludeUserClaimsInIdToken = true,
                    AllowOfflineAccess = true,
                    AbsoluteRefreshTokenLifetime = 31536000, //365 days
                        AccessTokenLifetime = 31536000, //365 days
                        AuthorizationCodeLifetime = 300,
                    IdentityTokenLifetime = 300,
                    RequireConsent = false,
                    FrontChannelLogoutUri = frontChannelLogoutUri,
                    RequireClientSecret = requireClientSecret,
                    RequirePkce = requirePkce
                },
                autoSave: true
            );
        }

        foreach (var scope in scopes)
        {
            if (client.FindScope(scope) == null)
            {
                client.AddScope(scope);
            }
        }

        foreach (var grantType in grantTypes)
        {
            if (client.FindGrantType(grantType) == null)
            {
                client.AddGrantType(grantType);
            }
        }

        if (!secret.IsNullOrEmpty())
        {
            if (client.FindSecret(secret) == null)
            {
                client.AddSecret(secret);
            }
        }

        if (redirectUri != null)
        {
            if (client.FindRedirectUri(redirectUri) == null)
            {
                client.AddRedirectUri(redirectUri);
            }
        }

        if (postLogoutRedirectUri != null)
        {
            if (client.FindPostLogoutRedirectUri(postLogoutRedirectUri) == null)
            {
                client.AddPostLogoutRedirectUri(postLogoutRedirectUri);
            }
        }

        if (permissions != null)
        {
            await _permissionDataSeeder.SeedAsync(
                ClientPermissionValueProvider.ProviderName,
                name,
                permissions,
                null
            );
        }

        if (corsOrigins != null)
        {
            foreach (var origin in corsOrigins)
            {
                if (!origin.IsNullOrWhiteSpace() && client.FindCorsOrigin(origin) == null)
                {
                    client.AddCorsOrigin(origin);
                }
            }
        }

        return await _clientRepository.UpdateAsync(client);
    }
}