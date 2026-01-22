namespace Trials
{
    // Define the types of trials that occur during the experiment timeline
    public enum ETrialType
    {
        Fit = 1,
        Setup = 2,
    };

    public interface ITrial
    {
        // Method called prior to running Trial
        public void OnStart();
        
        // Default method that defines Trial behavior, manages stimuli etc.
        public void Run();
        
        // Method called at the end of the Trial
        public void OnEnd();
    }
}