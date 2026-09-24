namespace GarageSaas.Exceptions
{
    public class GarageAccessException : System.Exception
    {
        public GarageAccessException(string message)
            : base(message)
        {
        }
    }
}