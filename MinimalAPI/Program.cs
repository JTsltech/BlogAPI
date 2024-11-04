using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MinimalAPI.Contracts;
using MinimalAPI.Middleware;
using MinimalAPI.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<BambooDB>(options => {
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),options1 => options1.EnableRetryOnFailure(maxRetryCount: 5,
                    maxRetryDelay: System.TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null));
});


builder.Services.AddSingleton<TokenService>(new TokenService());
builder.Services.AddScoped<IUserRepository,UserRepository>();
builder.Services.AddScoped<IPostService,PostService>();
builder.Services.AddScoped<IPostRepository,PostRepository>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "JWT Authentication",
        Description = "Enter JWT Bearer token * *_only_ * *",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // must be lower case
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {securityScheme, new string[] { }}
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyAllowedOrigins",
        policy =>
        {
            policy.WithOrigins("localhost").AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
        });
});
var app = builder.Build();
app.UseCors("MyAllowedOrigins");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseAuthentication();
app.UseSession();


//JWT-Security-Login-method
app.MapPost("/login", [AllowAnonymous] async ([FromBody] UserLogin userModel, [FromServices] TokenService tokenService, [FromServices] IUserRepository userRepositoryService, HttpResponse response) => {
    var userDto = userRepositoryService.GetUser(userModel);
    if (userDto == null)
    {
        response.StatusCode = 401;
        return Results.NotFound();
    }
    var token = tokenService.BuildToken(builder.Configuration["Jwt:Key"], builder.Configuration["Jwt:Issuer"], builder.Configuration["Jwt:Audience"], userDto);
    //await response.WriteAsJsonAsync(new UserToken{ Token = token });
    return Results.Ok(new UserToken { Token = token });
}).Produces<UserToken>(StatusCodes.Status200OK)
.WithName("Login").WithTags("User");

//Insert
app.MapPost("/createAccount",
([FromBody] Person person, [FromServices] BambooDB db,[FromServices] IUserRepository userRepository, HttpResponse response) =>
{
    userRepository.CreateAccount(person);
    response.StatusCode = 200;
    response.Headers.Location = $"account/{person.Id}";
})
.Accepts<Person>("application/json")
.Produces<Person>(StatusCodes.Status201Created)
.WithName("AddNewPerson").WithTags("User");

//Fetch a single record using ID
app.MapGet("/account/{id}", [Authorize(AuthenticationSchemes = "Bearer")] async (BambooDB db, int id) =>
await db.Persons.SingleOrDefaultAsync(s => s.Id == id) is Person person ? Results.Ok(person) : Results.NotFound()
)
.Produces<Person>(StatusCodes.Status200OK)
.WithName("GetPersonbyID").WithTags("User");

//Fetch a single record using ID
app.MapGet("/currentUser", ([FromServices]IUserRepository repository) =>
    repository.GetCurrentUser() is Person person ? Results.Ok(person) : Results.NotFound()
)
.Produces<UserLogin>(StatusCodes.Status200OK)
.WithName("GetCurrentUser").WithTags("User");

//delete current session
app.MapGet("/deleteSession", ([FromServices] IUserRepository repository) =>
    repository.DeleteUserSession()
)
.Produces<UserLogin>(StatusCodes.Status200OK)
.WithName("DeleteUserSession").WithTags("User");

app.MapPost("/uploadfile",
    [Authorize(AuthenticationSchemes = "Bearer")] async Task<IResult> (HttpContext context,[FromServices] IPostService postService) =>{
        try
        {
            if (!context.Request.HasFormContentType)
                return Results.BadRequest();

            var fileData = context.Request.Form.Files[0];
            //var fileData = file;
            if (fileData is null || fileData.Length == 0)
                return Results.BadRequest();

            var fileDetails = new PostImages()
            {
                fileName = fileData.FileName,
                fileType = Path.GetExtension(fileData.FileName).Trim('.')
            };

            using (var stream = new MemoryStream())
            {
                fileData.CopyTo(stream);
                fileDetails.fileData = stream.ToArray();
            }

            var result = postService.AddFeaturedImage(fileDetails);

            return Results.Ok(result);
        }
        catch (Exception)
        {
            throw;
        }
    }).WithName("UploadFile").WithTags("Image Upload").Produces<PostImages>(StatusCodes.Status200OK);

//Preview Image by postId
app.MapGet("/postImage/{postId}",
   [Authorize] async Task<byte[]> (int postId, [FromServices] BambooDB db) =>
    {
        try
        {
            var featuredImage = db.Posts.Where(x=>x.Id == postId).FirstOrDefault().featuredImage;
            var imageId = new Guid(featuredImage);
            var file = db.PostImages!.Where(x => x.ImageId == imageId).FirstOrDefaultAsync();

            var content = new System.IO.MemoryStream(file.Result!.fileData);
            var path = Path.Combine(
               Directory.GetCurrentDirectory(), "uploads",
               file.Result.fileName);

            await CopyStream(content, path);

            return file.Result!.fileData;

        }
        catch (Exception)
        {
            throw;
        }
    }).WithTags("Image Upload");

//Preview Image by fileId
app.MapGet("/previewImage/{fileId}",
   [Authorize(AuthenticationSchemes = "Bearer")] async Task<byte[]> (Guid fileId, [FromServices] BambooDB db) =>
    {
        try
        {
            var imageId = fileId;
            var file = db.PostImages!.Where(x => x.ImageId == imageId).FirstOrDefaultAsync();

            var content = new System.IO.MemoryStream(file.Result!.fileData);
            var path = Path.Combine(
               Directory.GetCurrentDirectory(), "uploads",
               file.Result.fileName);

            await CopyStream(content, path);

            return file.Result!.fileData;

        }
        catch (Exception)
        {
            throw;
        }
    }).WithTags("Image Upload");

//Insert Post

app.MapPost("/addPost",
   [Authorize(AuthenticationSchemes = "Bearer")] async Task<IResult> ([FromBody] Post post,[FromServices] IUserRepository repository, [FromServices] IPostService postService, HttpResponse response) =>
    {
        try
        {
            var data = repository.GetCurrentUser();
            post.active = true;
            if (post.userId == 0)
            {
                post.userId = data.Id;
            }
            int postId = postService.CreatePost(post);

            response.StatusCode = 200;
            response.Headers.Location = $"post/{postId}";

            return Results.Ok(postId);

        }
        catch (Exception ex)
        {
            throw;
        }
        
    })
.Accepts<Post>("application/json")
.Produces<Post>(StatusCodes.Status201Created)
.WithName("NewPost").WithTags("Posts");

//Update Post

app.MapPost("/updatePost",
  [Authorize(AuthenticationSchemes = "Bearer")]  async Task<IResult> ([FromBody] Post post, [FromServices] IUserRepository repository, [FromServices] IPostService postService, HttpResponse response) =>
    {
        try
        {
            var data = repository.GetCurrentUser();
            post.active = true;
            if (post.userId == 0)
            {
                post.userId = data.Id;
            }
            int postId = postService.UpdatePost(post);

            response.StatusCode = 200;
            response.Headers.Location = $"post/{postId}";

            return Results.Ok(postId);

        }
        catch (Exception ex)
        {
            throw;
        }

    })
.Accepts<Post>("application/json")
.Produces<Post>(StatusCodes.Status200OK)
.WithName("UpdatePost").WithTags("Posts");

//delete post by Id
app.MapPost("/deletePost/{postId}", [Authorize(AuthenticationSchemes = "Bearer")](int postId,[FromServices] IPostService service) =>
    service.DeletePost(postId)
)
.Produces(StatusCodes.Status204NoContent)
.WithName("DeletePost").WithTags("Posts");

//Fetch a single record using ID
app.MapGet("/post/{id}",[Authorize(AuthenticationSchemes ="Bearer")] (int id,[FromServices] IPostService postService) =>

   postService.GetPostById(id) is Post post ? Results.Ok(post) : Results.NotFound()
)
.Produces<Post>(StatusCodes.Status200OK)
.WithName("GetPostbyID").WithTags("Posts");

//Fetch all posts
app.MapGet("/posts/allPosts", 
    IResult ([FromServices] IPostService postService) => 
    {
        List<Post> posts = postService.GetPosts();
        if(posts.Count > 0) return Results.Ok(posts);
        else return Results.BadRequest("no posts found");
    }
)
.Produces<Post>(StatusCodes.Status200OK)
.WithName("GetAllPosts").WithTags("Posts");

app.UseSwagger();
app.UseSwaggerUI();

app.Run();


static string CreateTempfilePath(string filename)
{
    var directoryPath = Path.Combine("D:\\My Work\\MinimalAPI\\MinimalAPI", "uploads");
    if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

    return Path.Combine(directoryPath, filename);
}

async Task<Person> GetCurrentUser()
{
    HttpClient httpClient = new HttpClient();
    httpClient.BaseAddress = new Uri("https://localhost:7034");
    var request = new HttpRequestMessage { RequestUri = new Uri("https://localhost:7034/currentUser"), Method = HttpMethod.Get };


    var res = await httpClient.SendAsync(request);
    var data = new Person();

    if(res.StatusCode == System.Net.HttpStatusCode.OK)
    {
        if (res is not null)
        {
            if (res.Content is not null)
            {
                using var responseStream = await res.Content.ReadAsStreamAsync();

                var serializer = new Newtonsoft.Json.JsonSerializer();
                using var streamReader = new StreamReader(responseStream);
                using var jsonTextReader = new JsonTextReader(streamReader);
                data = serializer.Deserialize<Person>(jsonTextReader);

            }
        }
    }   

    return data;
}

async Task<Guid> UploadFeaturedImage(IFormFile file,int postId,IPostService postService)
{
    //if (!context.Request.HasFormContentType)
    //    return Results.BadRequest();

    var fileData = file;//context.Request.Form.Files[0];

    if (fileData is null || fileData.Length == 0)
        Results.BadRequest();

    var fileDetails = new PostImages()
    {
        fileName = fileData.FileName,
        fileType = Path.GetExtension(fileData.FileName).Trim('.')
    };

    using (var stream = new MemoryStream())
    {
        fileData.CopyTo(stream);
        fileDetails.fileData = stream.ToArray();
    }

    var result = postService.AddFeaturedImage(fileDetails);

    return result;
}

async Task CopyStream(Stream stream, string downloadPath)
{
    using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
    {
        await stream.CopyToAsync(fileStream);
    }
}

HttpContent GetPostBodyContent(string postContentType, string mediaType, object postData)
{
    if (postData == null) return new StringContent(String.Empty);

    switch (postContentType)
    {
        case ContentTypeConstants.JSON:
            if (IsJson(postData.ToString()))
                return new StringContent((string)postData, Encoding.UTF8);
            else
            {
                var dataToSend = JsonConvert.SerializeObject(postData);

                return new StringContent((string)dataToSend, Encoding.UTF8, ContentTypeConstants.JSON);
            }

        case ContentTypeConstants.Form_multipart:
            return new FormUrlEncodedContent((Dictionary<string, string>)postData);
        case ContentTypeConstants.Form_x_www:
            return new FormUrlEncodedContent((Dictionary<string, string>)postData);
        case ContentTypeConstants.Stream:
            return new StreamContent((MemoryStream)postData);
        case ContentTypeConstants.String:
            return new StringContent((string)postData, Encoding.UTF8);
        default:
            return new StringContent((string)postData, Encoding.UTF8);
    }

}

static bool IsJson(string json)
{
    try
    {
        JToken.Parse(json);
        return true;
    }
    catch (JsonReaderException ex)
    {
        Trace.WriteLine((object?)ex);
        return false;
    }
}

public class FileUploadModel
{
    public IFormFile file { get; set; }
    public static async ValueTask<FileUploadModel?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var firstFile = context.Request.Form["file"];
        //var firstFile = form.Files["file"];

        return new FileUploadModel
        {
            //file = firstFile.
        };
    }
}

