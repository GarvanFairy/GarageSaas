namespace GarageSaas.Exceptions
{
    public class GarageAccessException : System.Exception
    {
        public bool SelectionRequired { get; }

        public GarageAccessException(string message, bool selectionRequired = false)
            : base(message)
        {
            SelectionRequired = selectionRequired;
        }
    }
}
