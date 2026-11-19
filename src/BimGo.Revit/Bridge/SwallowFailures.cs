namespace BimGo.Bridge
{
    /// <summary>Deletes warnings and rolls back a transaction on errors without showing a dialog.</summary>
    internal sealed class SwallowFailures : IFailuresPreprocessor
    {
        /// <summary>The first error's description, if any.</summary>
        public string FirstError { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            bool hasError = false;
            foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning)
                {
                    accessor.DeleteWarning(message);
                }
                else
                {
                    hasError = true;
                    FirstError ??= message.GetDescriptionText();
                }
            }
            return hasError ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
