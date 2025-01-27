namespace MatchBuddy.Core.Utilities.Results
{
    public class SuccessDataResult<T>: DataResult<T>
    {
        public SuccessDataResult(T data, string message):base(data,true,message)
        {
            
        }

        public SuccessDataResult(T data ): base(data,true)
        {
            
        }

        //Aşşağıdaki iki constructor çok kullanılmaz
        public SuccessDataResult(string message): base(default, true, message)
        {
            
        }

        public SuccessDataResult(): base(default,true) 
        {
            
        }
    }
}
