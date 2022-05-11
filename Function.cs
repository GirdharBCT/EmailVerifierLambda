using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EmailVerifier
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>

        public async Task<List<String[]>> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            LambdaLogger.Log("Lambda Hit ");
            var s3Event = evnt.Records?[0].S3;
            if (s3Event == null)
            {
                return null;
            }
            try
            {
                List<String[]> unVarifiedList = new List<String[]>();
                GetObjectResponse responseData = new GetObjectResponse();
                var request = new GetObjectRequest
                {
                    BucketName = s3Event.Bucket.Name,
                    Key = s3Event.Object.Key
                };
                responseData = await this.S3Client.GetObjectAsync(request);
                using (Stream responseStream = responseData.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    // The following outputs the content of my text file:
                    LambdaLogger.Log("Reading data from file.");
                    var x = reader.ReadLine();
                    Regex CSVParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
                    String[] DataList = CSVParser.Split(x);
                    LambdaLogger.Log($"Data list Header --> \n{ string.Join(",", DataList)} \n------------------");
                    var index = Array.FindIndex(DataList, x => x.Contains("Verified"));

                    while ((x = reader.ReadLine()) != null)
                    {
                        
                        DataList = CSVParser.Split(x);
                        LambdaLogger.Log($"Data list item --> \n{ string.Join(",", DataList)} \n------------------");
                        if (DataList[index].ToString() != "Yes")
                        {
                            unVarifiedList.Add(DataList);
                            LambdaLogger.Log($"unvarified list item --> \n{ unVarifiedList.ToString()} \n------------------");

                        }
                    }
                    // Do some magic to return content as a stream
                }


                return unVarifiedList;
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }
        }
    }
}
