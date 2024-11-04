using MinimalAPI.Models;

namespace MinimalAPI.Contracts
{
    public interface IPostRepository
    {
        int CreatePost(Post post);
        int UpdatePost(Post post);
        List<Post> GetPosts();
        void DeletePost(int postId);
        Post GetPostById(int postId);
        Guid AddFeaturedImage(PostImages image);
    }


    public class PostRepository : IPostRepository
    {
        private readonly BambooDB _db;
        public PostRepository(BambooDB db)
        {
            _db = db;
        }

        public Guid AddFeaturedImage(PostImages image)
        {
            _db.PostImages.Add(image);
            _db.SaveChanges();

            return image.ImageId;
        }

        public int CreatePost(Post post)
        {
            try
            {
                _db.Posts.Add(post);
                _db.SaveChanges();
            }
            catch (Exception ex)
            {

            }

            return post.Id;
        }

        public void DeletePost(int postId)
        {
            var post = _db.Posts.Where(x=>x.Id == postId).FirstOrDefault();
            if (post != null)
            {
                _db.Posts.Remove(post);
                _db.SaveChanges();
            }
            else
                throw new ApplicationException("post not found");
        }

        public Post GetPostById(int postId)
        {
            var post = _db.Posts.Where(x => x.Id == postId).FirstOrDefault();
            if (post != null)
                return post;
            else
                throw new ApplicationException("post not found");
        }

        public List<Post> GetPosts()
        {
            return _db.Posts.ToList();
        }

        public int UpdatePost(Post post)
        {
            try
            {
                _db.Posts.Update(post);
                _db.SaveChanges();
            }
            catch(Exception ex)
            {

            }

            return post.Id;
        }

    }
}
