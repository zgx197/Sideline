#nullable enable

using System;

namespace Sideline.Facet.Application
{
    /// <summary>
    /// Facet 应用层结果对象。
    /// </summary>
    public class AppResult
    {
        protected AppResult(bool isSuccess, string? errorCode, string? errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 是否成功。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 失败错误码。
        /// </summary>
        public string? ErrorCode { get; }

        /// <summary>
        /// 失败错误消息。
        /// </summary>
        public string? ErrorMessage { get; }

        public static AppResult Success()
        {
            return new AppResult(true, null, null);
        }

        public static AppResult Fail(string errorCode, string errorMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            return new AppResult(false, errorCode, errorMessage);
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"Fail({ErrorCode}): {ErrorMessage}";
        }
    }

    /// <summary>
    /// Facet 应用层泛型结果对象。
    /// </summary>
    public sealed class AppResult<TValue> : AppResult
    {
        private AppResult(bool isSuccess, TValue? value, string? errorCode, string? errorMessage)
            : base(isSuccess, errorCode, errorMessage)
        {
            Value = value;
        }

        /// <summary>
        /// 成功结果值。
        /// </summary>
        public TValue? Value { get; }

        public static AppResult<TValue> Success(TValue value)
        {
            return new AppResult<TValue>(true, value, null, null);
        }

        public static new AppResult<TValue> Fail(string errorCode, string errorMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            return new AppResult<TValue>(false, default, errorCode, errorMessage);
        }
    }
}
