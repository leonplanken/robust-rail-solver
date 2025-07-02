using ServiceSiteScheduling.Utilities;
using YamlDotNet.Serialization;
using Google.Protobuf;
using AlgoIface;
using System.Text.Json;
using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Collections;
using Microsoft.VisualBasic;


namespace ServiceSiteScheduling
{
    class Program
    {

        // Method: Run the program from a config file. This is the entry point of the application
        static void Main(string[] args)
        {

            if (args.Length != 0)
            {
                string config_file = "";
                foreach (string arg in args)
                {
                    if (arg.StartsWith("--config="))
                    {
                        config_file = arg.Substring("--config=".Length);


                        if (!File.Exists(config_file))
                        {
                            Console.Error.WriteLine($"Error: Config file '{config_file}' not found.");
                            Environment.Exit(1);
                        }

                        string yaml = File.ReadAllText(config_file);

                        var deserializer = new Deserializer();
                        Config config = deserializer.Deserialize<Config>(new StringReader(yaml));
                        if (config.Mode == "Standard")
                        {
                            Console.WriteLine("***************** Test_Location_Scenario_Parsing() *****************");

                            Test_Location_Scenario_Parsing(config.LocationPath, config.ScenarioPath);

                            Console.WriteLine("***************** CreatePlan() *****************");
                            CreatePlan(config.LocationPath, config.ScenarioPath, config.PlanPath, config);

                        }
                        else if (config.Mode == "DeepLook")
                        {
                            Console.WriteLine("***************** Test_Location_Scenario_Parsing() *****************");

                            Test_Location_Scenario_Parsing(config.LocationPath, config.ScenarioPath);



                            // Contains all the tested scenario cases and the plan evaluation results [valid, not valid]  
                            Dictionary<string, string> ResultSummary = new Dictionary<string, string>();
                            string scenarioTestCase = "";

                            int testCases = config.DeepLook.TestCases;

                            for (int testCase = 0; testCase < testCases; testCase++)
                            {

                                Console.WriteLine("***************** CreatePlan() *****************");
                                ProblemInstance.Current = ProblemInstance.ParseJson(config.LocationPath, config.ScenarioPath);

                                int numberOfArrivals = ProblemInstance.Current.ArrivalsOrdered.Count();

                                ulong timeToAjustCase0 = 0;
                                ulong timeToAjustCase1 = 0;
                                ulong timeToAjustCase2 = 0;

                                ulong consAmount = 100;

                                // It is a simple example in which the arrival and departure times are modified
                                switch (testCase % (numberOfArrivals + 1))
                                {
                                    case 0:
                                        // Test case: Increase the time of arrival trains, but not the departure trains' 
                                        timeToAjustCase0 = timeToAjustCase0 + consAmount;

                                        foreach (var arrivalTrain in ProblemInstance.Current.InterfaceScenario.In.Trains)
                                        {
                                            arrivalTrain.Arrival = arrivalTrain.Arrival + timeToAjustCase0;
                                            arrivalTrain.Departure = arrivalTrain.Departure + timeToAjustCase0;
                                        }
                                        break;
                                    case 1:
                                        // Test case: Increase the time of departure trains, but not the arrival trains'
                                        timeToAjustCase1 = timeToAjustCase1 + consAmount;
                                        foreach (var departureTrain in ProblemInstance.Current.InterfaceScenario.Out.TrainRequests)
                                        {
                                            departureTrain.Arrival = departureTrain.Arrival + timeToAjustCase1;
                                            departureTrain.Departure = departureTrain.Departure + timeToAjustCase1;
                                        }

                                        break;
                                    case 2:
                                        // Test case: Increase the time of arrival trains, and the departure trains'
                                        timeToAjustCase2 = timeToAjustCase2 + consAmount;
                                        foreach (var arrivalTrain in ProblemInstance.Current.InterfaceScenario.In.Trains)
                                        {
                                            arrivalTrain.Arrival = arrivalTrain.Arrival + timeToAjustCase2;
                                            arrivalTrain.Departure = arrivalTrain.Departure + timeToAjustCase2;
                                        }

                                        foreach (var departureTrain in ProblemInstance.Current.InterfaceScenario.Out.TrainRequests)
                                        {
                                            departureTrain.Arrival = departureTrain.Arrival + timeToAjustCase2;
                                            departureTrain.Departure = departureTrain.Departure + timeToAjustCase2;
                                        }

                                        break;
                                    default:

                                        break;
                                }

                                // The current instance has to be reinitialized since the scenario parameters were changend in a Protobuf object level according to the test cases 
                                ProblemInstance.Current = ProblemInstance.Parse(ProblemInstance.Current.InterfaceLocation, ProblemInstance.Current.InterfaceScenario);


                                // Convert the scenario into an evaluator type scenario
                                // and also store it with the prefix scenario_evaluator.json
                                Converter converter = new Converter(ProblemInstance.Current, config.DeepLook.ConversionAndStorage.PathScenarioEval);

                                if (converter.ConvertScenario())
                                {
                                    Console.WriteLine("----------------------------------------------------------------------");
                                    Console.WriteLine("Conversion done with success");
                                    Console.WriteLine("----------------------------------------------------------------------");

                                    // Store evaluator format scenarion per test case
                                    var fileName = "scenario_evaluator" + "_case_" + testCase;
                                    if (!converter.StoreScenarioEvaluator(fileName))
                                        Console.WriteLine("Error while storage of evaluator format scenario");

                                    // Store the modified solver format scenario in the same folder but under a different file name per test case
                                    var fileNameSolverScenario = "scenario_solver" + "_case_" + testCase;
                                    if (!converter.StoreScenarioSolver(fileNameSolverScenario))
                                        Console.WriteLine("Error while storage of solver format scenario");

                                    scenarioTestCase = fileNameSolverScenario;
                                }
                                // Create a plan corresponding to the scenario per test case
                                CreatePlanFromExisting(ProblemInstance.Current, config.PlanPath, config);

                                // Store the plan per test case
                                var fileNameToStorePlam = "plan" + "_case_" + testCase;
                                if (!converter.StorePlan(fileNameToStorePlam, config.DeepLook.EvaluatorInput.PathPlan))
                                    Console.WriteLine($"Plan Path {config.DeepLook.EvaluatorInput.PathPlan}");


                                bool evaluatorResult;
                                if (Call_Evaluator(config))
                                {
                                    Console.WriteLine("The plan is valid");
                                    evaluatorResult = true;
                                }
                                else
                                {
                                    Console.WriteLine("The plan is not valid");
                                    evaluatorResult = false;

                                }
                                if (StoreScenarioAndEvaluationResults(config.DeepLook.EvaluatorInput.PathScenario, config.DeepLook.ConversionAndStorage.PathScenarioEval, config.DeepLook.ConversionAndStorage.PathEvalResult, testCase, evaluatorResult))
                                {
                                    Console.WriteLine("Scenario for Evaluator and the Results are successfully stored");
                                }
                                else
                                {
                                    Console.WriteLine("Problemes during the storage of Scenario for Evaluator and the Results");
                                }

                                // Add summary of the test cases and evaluation results

                                ResultSummary[scenarioTestCase] = evaluatorResult ? "Valid ✅" : "Not Valid ❌";
                            }

                            // Print the sumarry of the test cases and their evaluation results
                            PrintSummary(ResultSummary);

                        }
                        else
                        {
                            Console.WriteLine("Unknown parameter for Mode");
                        }

                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown --parameter name");
                        Environment.Exit(1);
                    }
                }
            }

            else
            {
                Test_Location_Scenario_Parsing("./database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json", "./database/TUSS-Instance-Generator/setting_A/scenario_solver.json");
                Console.WriteLine("***************** CreatePlan() *****************");
                CreatePlan("./database/TUSS-Instance-Generator/scenario_settings/setting_A/location_solver.json", "/database/TUSS-Instance-Generator/setting_A/scenario_solver.json", "./database/TUSS-Instance-Generator/plan.json");
            }



        }

        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        //          @config: service site scheduling config to creat the plan from
        // Output:  @plan_path: path to where the plan (.json) file will be written
        // Method: First it calls a Tabu Search method to find an initial plan (Graph) that is used by 
        //         a Simulated Annealing method to find the final schedle plan (Totally Ordered Graph)
        static void CreatePlan(string location_path, string scenario_path, string plan_path, Config config = null)
        {

            Random random = new Random();
            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;

            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);

            int solved = 0;
            for (int i = 0; i < 1; i++)
            {
                Console.WriteLine(i);
                LocalSearch.TabuSearch ts = new LocalSearch.TabuSearch(random);
                if (config != null)
                {
                    ts.Run(config.TabuSearch.Iterations, config.TabuSearch.IterationsUntilReset, config.TabuSearch.TabuListLength, config.TabuSearch.Bias, config.TabuSearch.SuppressConsoleOutput);
                }
                else
                {
                    ts.Run(40, 100, 16, 0.5);
                }
                LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts.Graph);
                if (config != null)
                {
                    sa.Run(new Time(config.SimulatedAnnealing.MaxDuration), config.SimulatedAnnealing.StopWhenFeasible, config.SimulatedAnnealing.IterationsUntilReset, config.SimulatedAnnealing.T, config.SimulatedAnnealing.A, config.SimulatedAnnealing.Q, config.SimulatedAnnealing.Reset, config.SimulatedAnnealing.Bias, config.SimulatedAnnealing.SuppressConsoleOutput, config.SimulatedAnnealing.IintensifyOnImprovement);
                }
                else
                {
                    sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);

                }

                Console.WriteLine("--------------------------");
                Console.WriteLine(" Output Movement Schedule ");
                Console.WriteLine("--------------------------");

                sa.Graph.OutputMovementSchedule();
                Console.WriteLine("--------------------------");
                Console.WriteLine("");


                Console.WriteLine("----------------------------");
                Console.WriteLine(" Output Train Unit Schedule ");
                Console.WriteLine("----------------------------");
                Console.WriteLine("");
                sa.Graph.OutputTrainUnitSchedule();
                Console.WriteLine("----------------------------");

                Console.WriteLine("");
                Console.WriteLine("------------------------------");
                Console.WriteLine(" Output Constraint Violations ");
                Console.WriteLine("------------------------------");

                sa.Graph.OutputConstraintViolations();
                Console.WriteLine(sa.Graph.Cost);
                Console.WriteLine("--------------------------");

                if (sa.Graph.Cost.ArrivalDelays + sa.Graph.Cost.DepartureDelays + sa.Graph.Cost.TrackLengthViolations + sa.Graph.Cost.Crossings + sa.Graph.Cost.CombineOnDepartureTrack <= 2)
                {
                    solved++;
                }

                if (sa.Graph.Cost.BaseCost < (best?.BaseCost ?? double.PositiveInfinity))
                {
                    best = sa.Graph.Cost;
                    graph = sa.Graph;
                }
                Console.WriteLine($"solved: {solved}");
                Console.WriteLine($"best = {best}");
                Console.WriteLine("------------------------------");
                Console.WriteLine($"Generate JSON format plan");
                Console.WriteLine("------------------------------");

                Plan plan_pb = sa.Graph.GenerateOutputPB();

                string jsonPlan = JsonFormatter.Default.Format(plan_pb);

                // Console.WriteLine(jsonPlan);

                string directoryPath = Path.GetDirectoryName(plan_path);

                if (!Directory.Exists(directoryPath) && directoryPath != null)
                {

                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Directory created: {directoryPath}");
                }

                File.WriteAllText(plan_path, jsonPlan);

                Console.WriteLine("----------------------------------------------------------------------");


                sa.Graph.DisplayMovements();
                sa.Graph.Clear();

            }

            Console.WriteLine("------------ OVERALL BEST --------------");
            Console.WriteLine(best);

            // Console.ReadLine();
        }

        // Input:   @currentInstance: an already existing problem instance with scenario and location -
        //          useful in tests when scenarios have to be modifed at code level not directly in the .json file
        //          @config: service site scheduling config to creat the plan from
        // Output:  @plan_path: path to where the plan (.json) file will be written
        // Method: First it calls a Tabu Search method to find an initial plan (Graph) that is used by 
        //         a Simulated Annealing method to find the final schedle plan (Totally Ordered Graph)
        static void CreatePlanFromExisting(ProblemInstance currentInstance, string plan_path, Config config = null)
        {

            Random random = new Random();
            Solutions.SolutionCost best = null;
            Solutions.PlanGraph graph = null;

            ProblemInstance.Current = currentInstance;

            int solved = 0;
            for (int i = 0; i < 1; i++)
            {
                Console.WriteLine(i);
                LocalSearch.TabuSearch ts = new LocalSearch.TabuSearch(random);
                if (config != null)
                {
                    ts.Run(config.TabuSearch.Iterations, config.TabuSearch.IterationsUntilReset, config.TabuSearch.TabuListLength, config.TabuSearch.Bias, config.TabuSearch.SuppressConsoleOutput);
                }
                else
                {
                    ts.Run(40, 100, 16, 0.5);
                }
                LocalSearch.SimulatedAnnealing sa = new LocalSearch.SimulatedAnnealing(random, ts.Graph);
                if (config != null)
                {
                    sa.Run(new Time(config.SimulatedAnnealing.MaxDuration), config.SimulatedAnnealing.StopWhenFeasible, config.SimulatedAnnealing.IterationsUntilReset, config.SimulatedAnnealing.T, config.SimulatedAnnealing.A, config.SimulatedAnnealing.Q, config.SimulatedAnnealing.Reset, config.SimulatedAnnealing.Bias, config.SimulatedAnnealing.SuppressConsoleOutput, config.SimulatedAnnealing.IintensifyOnImprovement);
                }
                else
                {
                    sa.Run(Time.Hour, true, 150000, 15, 0.97, 2000, 2000, 0.2, false);

                }

                Console.WriteLine("--------------------------");
                Console.WriteLine(" Output Movement Schedule ");
                Console.WriteLine("--------------------------");

                sa.Graph.OutputMovementSchedule();
                Console.WriteLine("--------------------------");
                Console.WriteLine("");


                Console.WriteLine("----------------------------");
                Console.WriteLine(" Output Train Unit Schedule ");
                Console.WriteLine("----------------------------");
                Console.WriteLine("");
                sa.Graph.OutputTrainUnitSchedule();
                Console.WriteLine("----------------------------");

                Console.WriteLine("");
                Console.WriteLine("------------------------------");
                Console.WriteLine(" Output Constraint Violations ");
                Console.WriteLine("------------------------------");

                sa.Graph.OutputConstraintViolations();
                Console.WriteLine(sa.Graph.Cost);
                Console.WriteLine("--------------------------");

                if (sa.Graph.Cost.ArrivalDelays + sa.Graph.Cost.DepartureDelays + sa.Graph.Cost.TrackLengthViolations + sa.Graph.Cost.Crossings + sa.Graph.Cost.CombineOnDepartureTrack <= 2)
                {
                    solved++;
                }

                if (sa.Graph.Cost.BaseCost < (best?.BaseCost ?? double.PositiveInfinity))
                {
                    best = sa.Graph.Cost;
                    graph = sa.Graph;
                }
                Console.WriteLine($"solved: {solved}");
                Console.WriteLine($"best = {best}");
                Console.WriteLine("------------------------------");
                Console.WriteLine($"Generate JSON format plan");
                Console.WriteLine("------------------------------");

                Plan plan_pb = sa.Graph.GenerateOutputPB();

                string jsonPlan = JsonFormatter.Default.Format(plan_pb);

                // Console.WriteLine(jsonPlan);

                string directoryPath = Path.GetDirectoryName(plan_path);

                if (!Directory.Exists(directoryPath) && directoryPath != null)
                {

                    Directory.CreateDirectory(directoryPath);
                    Console.WriteLine($"Directory created: {directoryPath}");
                }

                File.WriteAllText(plan_path, jsonPlan);

                Console.WriteLine("----------------------------------------------------------------------");


                sa.Graph.DisplayMovements();
                sa.Graph.Clear();

            }

            Console.WriteLine("------------ OVERALL BEST --------------");
            Console.WriteLine(best);

            // Console.ReadLine();
        }

        static void Test()
        {

            try
            {

                AlgoIface.Location location;
                using (var input = File.OpenRead("database/TUSS-Instance-Generator/location.json"))
                    location = AlgoIface.Location.Parser.ParseFrom(input);

                Console.WriteLine("Location:");

                string json = JsonFormatter.Default.Format(location);

                // Console.WriteLine("JSON: \n " + json);


                byte[] locationBytes = location.ToByteArray();
                Console.WriteLine("Location :" + locationBytes.Length);

                Console.WriteLine(Convert.ToBase64String(location.ToByteArray()));

                var location_TrackParts = location.TrackParts;

                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed message is null.");

                }

                foreach (AlgoIface.TrackPart trackType in location_TrackParts)
                {
                    Console.WriteLine("ID : " + trackType.Id);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

        }

        static bool StoreScenarioAndEvaluationResults(string PathScenarioForEval, string PathToStoreEvalScenario, string PathToEvaluationResult, int TestNum, bool valid)
        {

            try
            {

                string destinationPath = PathToStoreEvalScenario;

                destinationPath = destinationPath + "/" + "scenario_case_" + TestNum + (valid ? "_valid" : "_not_valid") + Path.GetExtension(PathScenarioForEval);
                File.Copy(PathScenarioForEval, destinationPath, overwrite: true);

                destinationPath = PathToStoreEvalScenario;
                destinationPath = destinationPath + "/" + "evaluator_result_case_" + TestNum + (valid ? "_valid" : "_not_valid") + Path.GetExtension(PathToEvaluationResult);

                // destinationPath = Path.GetFileNameWithoutExtension(PathToEvaluationResult);
                // destinationPath = destinationPath + "evaluator_result_case_" + TestNum + (valid ? "valid" : "not_valid") + Path.GetExtension(PathToStoreEvalScenario);
                File.Copy(PathToEvaluationResult, destinationPath, overwrite: true);


            }
            catch (IOException ex)
            {
                Console.WriteLine("Error copying file: " + ex.Message);
                return false;
            }



            return true;
        }

        static bool Call_Evaluator(Config config)
        {
            Process process = new Process();
            process.StartInfo.FileName = config.DeepLook.EvaluatorInput.Path;

            if (config.DeepLook.EvaluatorInput.Mode == "EVAL")
            {
                process.StartInfo.Arguments = "--mode " + config.DeepLook.EvaluatorInput.Mode + " --path_location " + config.DeepLook.EvaluatorInput.PathLocation + " --path_scenario " + config.DeepLook.EvaluatorInput.PathScenario + " --path_plan " + config.DeepLook.EvaluatorInput.PathPlan + " --plan_type " + config.DeepLook.EvaluatorInput.PlanType;
            }
            else if (config.DeepLook.EvaluatorInput.Mode == "EVAL_AND_STORE")
            {
                process.StartInfo.Arguments = "--mode " + config.DeepLook.EvaluatorInput.Mode + " --path_location " + config.DeepLook.EvaluatorInput.PathLocation + " --path_scenario " + config.DeepLook.EvaluatorInput.PathScenario + " --path_plan " + config.DeepLook.EvaluatorInput.PathPlan + " --plan_type " + config.DeepLook.EvaluatorInput.PlanType + " --path_eval_result " + config.DeepLook.ConversionAndStorage.PathEvalResult;

            }
            else
            {
                Console.WriteLine("Warning ! Mode is unknown");
            }
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine("Output from Evaluator:");
            Console.WriteLine(output);


            // Check the evaluator's result if it is valid plan or not only in case the resutls were stored


            if (output.Contains("The plan is valid", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;




        }

        // Tests if the given location and scenario (json format) files can be parsed correctly int protobuf objects (ProblemInstance)
        // As partial results, the function displays the details about the infrstructure of the location, and the incoming and outgoing trains of the scenario
        // Input:   @location_path: path to the location (.json) file
        //          @scenario_path: path to the scenario (.json) file
        static void Test_Location_Scenario_Parsing(string location_path, string scenario_path)
        {

            ProblemInstance.Current = ProblemInstance.ParseJson(location_path, scenario_path);
            try
            {
                var location_TrackParts = ProblemInstance.Current.InterfaceLocation.TrackParts;

                if (location_TrackParts == null)
                {
                    throw new NullReferenceException("Parsed location is null.");

                }

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceLocation);

                string json_original = ProblemInstance.ParseJsonToString(location_path);


                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Location file parsing was successful !");
                    // Console.WriteLine("JSON: \n " + json_parsed);
                }
                else
                {
                    Console.WriteLine("The Location file parsing was not successful! ");
                }
            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

            try
            {

                string json_parsed = JsonFormatter.Default.Format(ProblemInstance.Current.InterfaceScenario);

                var scenario_in = ProblemInstance.Current.InterfaceScenario.In;
                var scenario_out = ProblemInstance.Current.InterfaceScenario.Out;

                if (scenario_in == null)
                {
                    throw new NullReferenceException("Parsed scenario in filed is null.");
                }


                if (scenario_out == null)
                {
                    throw new NullReferenceException("Parsed scenario out field is null.");
                }

                string json_original = ProblemInstance.ParseJsonToString(scenario_path);

                var token_parsed = JsonDocument.Parse(json_parsed);
                var token_original = JsonDocument.Parse(json_original);

                if (token_parsed.ToString() == token_parsed.ToString())
                {
                    Console.WriteLine("The Scenario file parsing was successful !");
                    // Console.WriteLine("JSON: \n " + json_parsed);
                }
                else
                {
                    Console.WriteLine("The Location file parsing was not successful! ");
                }

                Console.WriteLine("Scenario details: ");
                Console.WriteLine("---- Incoming Trains ----");
                List<AlgoIface.IncomingTrain> incomingTrains = new List<AlgoIface.IncomingTrain>(scenario_in.Trains);
                foreach (AlgoIface.IncomingTrain train in scenario_in.Trains)
                {
                    incomingTrains.Add(train);
                }

                Console.WriteLine("---- Outgoing Trains ----");
                foreach (AlgoIface.IncomingTrain train in incomingTrains)
                {
                    Console.WriteLine("Parcking track " + train.FirstParkingTrackPart + " for train (id) " + train.Id);
                }

                List<AlgoIface.TrainRequest> outgoingTrains = new List<AlgoIface.TrainRequest>(scenario_out.TrainRequests);
                foreach (AlgoIface.TrainRequest train in scenario_out.TrainRequests)
                {
                    outgoingTrains.Add(train);
                }

                foreach (AlgoIface.TrainRequest train in outgoingTrains)
                {
                    Console.WriteLine("Parcking track " + train.LastParkingTrackPart + " for train (id) " + train.DisplayName);

                }


            }
            catch (Exception e)
            {
                throw new ArgumentException("error during parsing", e);
            }

        }

        // Prints out all the scenario test cases and their evaluation results 
        static void PrintSummary(Dictionary<string, string> summary)
        {
            Console.WriteLine("+------------------------------------------------------+");
            Console.WriteLine("|                    Test summary                      |");
            Console.WriteLine("+------------------------------------------------------+");

            foreach (var item in summary)
            {
                Console.WriteLine($"|       {item.Key}        |  {item.Value}  |");
                Console.WriteLine("+______________________________________________________+");


            }
            Console.WriteLine("+------------------------------------------------------+");
            
        }


    }

    class Config
    {
        public ConfigTabuSearch TabuSearch { get; set; }
        public ConfigSimulatedAnnealing SimulatedAnnealing { get; set; }

        public ConfigDeepLook DeepLook { get; set; }
        public class ConfigTabuSearch
        {
            public int Iterations { get; set; }
            public int IterationsUntilReset { get; set; }
            public int TabuListLength { get; set; }
            public float Bias { get; set; }
            public bool SuppressConsoleOutput { get; set; }

        }
        public class ConfigSimulatedAnnealing
        {
            public int MaxDuration { get; set; }
            public bool StopWhenFeasible { get; set; }
            public int IterationsUntilReset { get; set; }
            public int T { get; set; }
            public float A { get; set; }
            public int Q { get; set; }
            public int Reset { get; set; }
            public float Bias { get; set; }
            public bool SuppressConsoleOutput { get; set; }
            public bool IintensifyOnImprovement { get; set; }

        }

        public class ConfigEvaluatorInput
        {
            // Path to the evaluator's executable
            public string Path { get; set; }
            // Mode to choose, simple evaluation or evaluation with storage of the results (recommended)
            public string Mode { get; set; }

            // # Folder where the evaluator format location file can be found (TODO: later the solver format location should be converted to evaluator format)
            public string PathLocation { get; set; }

            // Path to scenario_evaluator.json file ! Important ! scenario_evaluator.json is generated by the Deep Look mode from the solver format scenarion specified by ScenarioPath in the config.yaml 
            public string PathScenario { get; set; }

            // The path to the plan generated by the solver
            public string PathPlan { get; set; }

            // To tell the evaluator that a solver formated plan must be evaluated
            public string PlanType { get; set; }
        }

        public class ConfigConversionAndStorage
        {
            // The path where the evaluation results (.txt) should be stored 
            public string PathScenarioEval { get; set; }
            // The path where the scenario_evaluator.json will be stored after the conversion. Note that the name "scenario_evaluator" is a default name it can be changed in the code, but in that case the PathScenario should point to the "renamed" evaluator format scenarion file. This path also serves for serving the evaluator format scenarios after the evaluation as scenario_case_x_valid/not_valid.json - needed to summaize the run cases.
            public string PathEvalResult { get; set; }
        }
        public class ConfigDeepLook
        {
            public int TestCases { get; set; } 
            public ConfigEvaluatorInput EvaluatorInput { get; set; }

            public ConfigConversionAndStorage ConversionAndStorage { get; set; }


        }
        public int Seed { get; set; }
        public int MaxDuration { get; set; }
        public bool StopWhenFeasible { get; set; }
        public string LocationPath { get; set; }
        public string ScenarioPath { get; set; }
        public string PlanPath { get; set; }
        public string OutputPath { get; set; }
        public string Mode { get; set; }

    }
}
