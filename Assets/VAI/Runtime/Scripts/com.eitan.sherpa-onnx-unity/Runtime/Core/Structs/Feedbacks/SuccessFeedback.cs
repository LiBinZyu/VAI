using System;

namespace Eitan.SherpaOnnxUnity.Runtime
{
    public class SuccessFeedback : SherpaFeedback
    {
        public SuccessFeedback(SherpaOnnxModelMetadata metadata, string message, Exception exception = null) : base(metadata, message, exception)
        {
        }

        public override void Accept(ISherpaFeedbackHandler handler) => handler.OnFeedback(this);
    }
}