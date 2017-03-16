using System;

namespace Lithnet.Moveuser
{
    public class MoveUserException : Exception
    {
        public MoveUserException()
            : base()
        {
        }

        public MoveUserException(string message)
            : base(message)
        {
        }

        public MoveUserException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
