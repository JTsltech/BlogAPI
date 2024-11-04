using MinimalAPI.Models;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using MinimalAPI.Extensions;
using BCrypt.Net;
using Microsoft.Extensions.Caching.Memory;

namespace MinimalAPI.Contracts
{
    public interface IUserRepository
    {
        Person? GetCurrentUser();
        Person? GetUser(UserLogin user);
        int CreateAccount(Person person);
        void DeleteUserSession();
    }

    public class UserRepository : IUserRepository
    {
        private readonly BambooDB _db;
        private readonly ISession _session;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMemoryCache _cache;
        public UserRepository(BambooDB db, IHttpContextAccessor accessor,IMemoryCache cache)
        {
            _db = db;
            _httpContextAccessor = accessor;
            _session = _httpContextAccessor.HttpContext.Session;
            _cache = cache;
            //_session.InitObjectStore();
        }
        public int CreateAccount(Person person)
        {
            if(person != null)
            {
                person.Password = BCrypt.Net.BCrypt.HashPassword(person.Password);
                _db.Persons.Add(person);
                _db.SaveChanges();
                _session.SetString("CurrentUser", person.Id.ToString());

                _cache.Set("sessionCache", _session.GetString("CurrentUser"));
                
                return person.Id;
            }
            else return 0;
        }

        public Person? GetCurrentUser()
        {
            var userId = "";
            var person = new Person();
            if (_cache.TryGetValue("sessionCache", out userId))
            {
                //var value = _cache.Get("sessionCache");
                //userId = value == null ? default : value.ToString();
                person = _db.Persons.Where(x => x.Id == Convert.ToInt32(userId)).FirstOrDefault();
            }
            
            return person;
        }

        public Person? GetUser(UserLogin user)
        {
            Person person = _db.Persons.FirstOrDefault(x => x.Email == user.Email)!;

            if (person == null || !BCrypt.Net.BCrypt.Verify(user.Password, person.Password))
                    throw new ApplicationException("Username or password is incorrect");
            else
            {
                _session.SetString("CurrentUser", person.Id.ToString());

                _cache.Set("sessionCache", _session.GetString("CurrentUser"));
                //if (_db.UserLogins.FirstOrDefault(x => x.Name == person.Uname) == null)
                //{
                //    _db.UserLogins.Add(new UserLogin { Name = person.Uname, Password = person.Password });
                //    _db.SaveChanges();
                //}

                return person;
            }
        }

        public void DeleteUserSession()
        {
            var userId = "";
            if (_cache.TryGetValue("sessionCache", out userId))
            {
                _cache.Remove("sessionCache");
                if (_session.Keys.Contains("CurrentUser"))
                {
                    _session.Remove("CurrentUser");
                }
            }
        }
    }
}
