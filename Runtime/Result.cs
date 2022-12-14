namespace System.Net.Sockets
{
    /// <summary>
    /// Socket operation result.
    /// </summary>
    public class Result
    {
        #region Fields
        /// <summary>
        /// Socket operation succeeded?
        /// </summary>
        public bool Success { get; } = false;
        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; } = string.Empty;

        /// <summary>
        /// Shorthand <see cref="Success"/> inverse.
        /// </summary>
        public bool Failure => !Success;
        #endregion

        #region Constructors
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected Result(bool success, string error)
        {
            Success = success;
            Error = error;
        }
        #endregion

        #region State
        /// <summary>
        /// Returns a successful socket operation.
        /// </summary>
        public static Result Ok()
        {
            return new Result(true, string.Empty);
        }
        /// <summary>
        /// Returns a failed socket operation.
        /// </summary>
        /// <param name="message">Error message.</param>
        public static Result Fail(string message)
        {
            return new Result(false, message);
        }
        #endregion
    }

    /// <summary>
    /// Socket operation result.
    /// </summary>
    public class Result<T> : Result
    {
        #region Fields
        /// <summary>
        /// Object returned by socket operation.
        /// </summary>
        public T Value { get; } = default;
        #endregion

        #region Constructors
        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        protected internal Result(T value, bool success, string error) : base(success, error)
        {
            Value = value;
        }
        #endregion

        #region State
        /// <summary>
        /// Returns a successful socket operation.
        /// </summary>
        /// <param name="value">Object returned by socket operation.</param>
        public static Result<T> Ok(T value)
        {
            return new Result<T>(value ?? default!, true, string.Empty);
        }
        /// <summary>
        /// Returns a failed socket operation.
        /// </summary>
        /// <param name="value">Object returned by socket operation.</param>
        /// <param name="message">Error message.</param>
        public static Result<T> Fail(T value, string message)
        {
            return new Result<T>(value ?? default!, false, message);
        }
        #endregion
    }
}