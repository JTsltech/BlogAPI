using MinimalAPI.Models;

namespace MinimalAPI.Contracts
{
    public interface IPostService
    {
        int CreatePost(Post post);
        int UpdatePost(Post post);
        List<Post> GetPosts();
        void DeletePost(int postId);
        Post GetPostById(int postId);
        Guid AddFeaturedImage(PostImages image);

    }

    public class PostService : IPostService
    {
        private readonly IPostRepository _postRepository;
        public PostService(IPostRepository postRepository)
        {
            _postRepository = postRepository;
        }
        public Guid AddFeaturedImage(PostImages image)
        {
           return _postRepository.AddFeaturedImage(image);
        }

        public int CreatePost(Post post)
        {
            return _postRepository.CreatePost(post);
        }

        public void DeletePost(int postId)
        {
            _postRepository.DeletePost(postId);
        }

        public Post GetPostById(int postId)
        {
            try
            {
                return _postRepository.GetPostById(postId);
            }
            catch(Exception ex)
            {
                throw;
            }
        }

        public List<Post> GetPosts()
        {
            return _postRepository.GetPosts();
        }

        public int UpdatePost(Post post)
        {
            return _postRepository.UpdatePost(post);  
        }
    }
}
