﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Evolutionary
{
    public class Engine<T,S> where S : new()    // T: return type requested.  S : datatype for state information (must implement 0-param constructor)
    {
        // the engine parameters
        private int TourneySize                             = EngineParameterDefaults.TourneySize;
        private int StagnantGenerationLimit                 = EngineParameterDefaults.StagnantGenerationLimit;
        private double ElitismRate                          = EngineParameterDefaults.ElitismRate;
        private int PopulationSize                          = EngineParameterDefaults.PopulationSize;
        private bool IsLowerFitnessBetter                   = EngineParameterDefaults.IsLowerFitnessBetter;
        private double CrossoverRate                        = EngineParameterDefaults.CrossoverRate;
        private double MutationRate                         = EngineParameterDefaults.MutationRate;
        private int RandomTreeMinDepth                      = EngineParameterDefaults.RandomTreeMinDepth;
        private int RandomTreeMaxDepth                      = EngineParameterDefaults.RandomTreeMaxDepth;
        private int MinGenerations                          = EngineParameterDefaults.MinGenerations;
        private int MaxGenerations                          = EngineParameterDefaults.MaxGenerations;
        private SelectionStyle SelectionStyle               = EngineParameterDefaults.SelectionStyle;

        // object to store all available node contents (functions, constants, variables, etc.)
        private EngineComponents<T> primitiveSet = new EngineComponents<T>();

        // declare the delegate type for the fitness function, and define a pointer for it
        private delegate float FitnessFunctionPointer(CandidateSolution<T,S> tree);
        FitnessFunctionPointer myFitnessFunction;

        // declare the delegate type for the progress reporting function, and define a pointer for it
        private delegate bool ProgressFunctionPointer(EngineProgress progress);
        ProgressFunctionPointer myProgressFunction;

        private delegate bool ProgressWithCandidateFunctionPointer(EngineProgress progress, CandidateSolution<T,S> bestThisGeneration);
        ProgressWithCandidateFunctionPointer myProgressWithCandidateFunction;

        private List<CandidateSolution<T,S>> currentGeneration = new List<CandidateSolution<T,S>>();

        public float totalFitness;
        public int currentGenerationNumber;
        public int currentCandidateNumber;
        public float bestFitnessScoreAllTime;
        public float bestAverageFitnessScore;
        public List<float> generationsAverageFitnessScore; 
        public CandidateSolution<T,S> bestTreeAllTime;
        public int bestSolutionGenerationNumber, bestAverageFitnessGenerationNumber;
        private int numElitesToAdd;
        private bool needToSortByFitness;
        public Stopwatch timer;

        public delegate T ScanTreeFuncPointer(CandidateSolution<T,S> candidate, dynamic envInfo);
        ScanTreeFuncPointer myScanTreeFunc;

        public Engine()
        {
        }

        public Engine(EngineParameters userParams)
        {
            if (userParams.CrossoverRate.HasValue)                          CrossoverRate = userParams.CrossoverRate.Value;
            if (userParams.ElitismRate.HasValue)                            ElitismRate = userParams.ElitismRate.Value;
            if (userParams.IsLowerFitnessBetter.HasValue)                   IsLowerFitnessBetter = userParams.IsLowerFitnessBetter.Value;
            if (userParams.MaxGenerations.HasValue)                         MaxGenerations = userParams.MaxGenerations.Value;
            if (userParams.MinGenerations.HasValue)                         MinGenerations = userParams.MinGenerations.Value;
            if (userParams.MutationRate.HasValue)                           MutationRate = userParams.MutationRate.Value;
            if (userParams.StagnantGenerationLimit.HasValue)  StagnantGenerationLimit = userParams.StagnantGenerationLimit.Value;
            if (userParams.PopulationSize.HasValue)                         PopulationSize = userParams.PopulationSize.Value;
            if (userParams.RandomTreeMaxDepth.HasValue)                     RandomTreeMaxDepth = userParams.RandomTreeMaxDepth.Value;
            if (userParams.RandomTreeMinDepth.HasValue)                     RandomTreeMinDepth = userParams.RandomTreeMinDepth.Value;
            if (userParams.TourneySize.HasValue)                            TourneySize = userParams.TourneySize.Value;
            if (userParams.SelectionStyle.HasValue)                         SelectionStyle = userParams.SelectionStyle.Value;
        }

        public void InitTrainer()
        {
            bestFitnessScoreAllTime = (IsLowerFitnessBetter ? float.MaxValue : float.MinValue);
            bestAverageFitnessScore = (IsLowerFitnessBetter ? float.MaxValue : float.MinValue);
            bestTreeAllTime = null;
            bestSolutionGenerationNumber = 0;
            bestAverageFitnessGenerationNumber = 0;

            // elitism
            numElitesToAdd = (int)(ElitismRate * PopulationSize);

            // depending on whether elitism is used, or the selection type, we may need to sort candidates by fitness (which is slower)
            needToSortByFitness =
                SelectionStyle == SelectionStyle.RouletteWheel ||
                SelectionStyle == SelectionStyle.Ranked ||
                ElitismRate > 0;

            // create an initial population of random trees, passing the possible functions, consts, and variables
            for (int p = 0; p < PopulationSize; p++)
            {
                CandidateSolution<T,S> tree = new CandidateSolution<T,S>(primitiveSet, RandomTreeMinDepth, RandomTreeMaxDepth);
                tree.CreateRandom();
                currentGeneration.Add(tree);
            }

            currentGenerationNumber = 0;
            currentCandidateNumber = 0;
            generationsAverageFitnessScore = new List<float>();
            timer = new Stopwatch();
            timer.Restart();
        }

        private CandidateSolution<T,S> GetCurrentCandidate(){
            return currentGeneration[currentCandidateNumber];
        } 

        public dynamic ScanTree(dynamic envInfo)
        {
            var candidate = GetCurrentCandidate();
            return myScanTreeFunc(candidate, envInfo);
        }
    
        //IEC用に書き換え
        public void ChangeNextCandidate(){
            CandidateSolution<T,S> candidate = GetCurrentCandidate();
            currentCandidateNumber++;
        }

        //IEC用に追加したカスタム関数
        public void RegisterFitness(List<float> fitnesses){
            if(fitnesses.Count != currentGeneration.Count){
                UnityEngine.Debug.Log(fitnesses.Count);
                UnityEngine.Debug.Log(currentGeneration.Count);
                throw new Exception();
            }
            for (int i = 0; i < fitnesses.Count; i++)
            {
                currentGeneration[i].Fitness = fitnesses[i];
            }
            bool _ = ChangeNextGeneration();
        }

        //木構造を可視化するための関数
        public void VisTree(){
            UnityEngine.Debug.LogFormat("Tree0: {0}\nTree1: {1}\nTree2: {2}\nTree3: {3}\nTree4: {4}\nTree5: {5}\nTree6: {6}\nTree7: {7}\nTree8: {8}\n",currentGeneration[0],currentGeneration[1],currentGeneration[2],currentGeneration[3],currentGeneration[4],currentGeneration[5],currentGeneration[6],currentGeneration[7],currentGeneration[8]);
        }

        private bool ChangeNextGeneration(){
            // now check if we have a new best
            float bestFitnessScoreThisGeneration = (IsLowerFitnessBetter ? float.MaxValue : float.MinValue);
            CandidateSolution<T, S> bestTreeThisGeneration = null;
            totalFitness = 0;
            foreach (var tree in currentGeneration)
            {
                totalFitness += tree.Fitness;

                // find best of this generation, update best all-time if needed
                bool isBestThisGeneration = false;
                if (IsLowerFitnessBetter)
                    isBestThisGeneration = tree.Fitness < bestFitnessScoreThisGeneration;
                else
                    isBestThisGeneration = tree.Fitness > bestFitnessScoreThisGeneration;

                if (isBestThisGeneration)
                {
                    bestFitnessScoreThisGeneration = tree.Fitness;
                    bestTreeThisGeneration = tree;

                    bool isBestEver = false;
                    if (IsLowerFitnessBetter)
                        isBestEver = bestFitnessScoreThisGeneration < bestFitnessScoreAllTime;
                    else
                        isBestEver = bestFitnessScoreThisGeneration > bestFitnessScoreAllTime;

                    if (isBestEver)
                    {
                        bestFitnessScoreAllTime = bestFitnessScoreThisGeneration;
                        bestTreeAllTime = tree.Clone();
                        bestSolutionGenerationNumber = currentGenerationNumber;
                    }
                }
            }

            // determine average fitness and store if it's all-time best
            float averageFitness = totalFitness / PopulationSize;
            generationsAverageFitnessScore.Add(averageFitness);

            UnityEngine.Debug.LogFormat("[Generation {0}] Average fitness: {1}, Best fitness: {2}, Best tree: {3}", currentGenerationNumber,
                                                                                                                    averageFitness,
                                                                                                                    bestFitnessScoreThisGeneration, 
                                                                                                                    bestTreeThisGeneration);

            if (IsLowerFitnessBetter)
            {
                if (averageFitness < bestAverageFitnessScore)
                {
                    bestAverageFitnessGenerationNumber = currentGenerationNumber;
                    bestAverageFitnessScore = averageFitness;
                }
            }
            else
            {
                if (averageFitness > bestAverageFitnessScore)
                {
                    bestAverageFitnessGenerationNumber = currentGenerationNumber;
                    bestAverageFitnessScore = averageFitness;
                }
            }

            /*
            // report progress back to the user, and allow them to terminate the loop
            EngineProgress progress = new EngineProgress()
            {
                GenerationNumber = currentGenerationNumber,
                AvgFitnessThisGen = averageFitness,
                BestFitnessThisGen = bestFitnessScoreThisGeneration,
                BestFitnessSoFar = bestFitnessScoreAllTime,
                TimeForGeneration = timer.Elapsed
            };

            // there are two forms here, and if they aren't defined, this code just drops through
            bool keepGoing = true;
            if (myProgressFunction != null)
                myProgressFunction(progress);
            if (myProgressWithCandidateFunction != null)
                keepGoing = myProgressWithCandidateFunction(progress, bestTreeThisGeneration);
            if (!keepGoing) return true;  // user signalled to end looping

            // termination conditions
            if (currentGenerationNumber >= MinGenerations)
            {
                // exit the loop if we're not making any progress in our average fitness score or our overall best score
                if (((currentGenerationNumber - bestAverageFitnessGenerationNumber) >= StagnantGenerationLimit) &&
                    ((currentGenerationNumber - bestSolutionGenerationNumber) >= StagnantGenerationLimit))
                    return true;

                // maxed out?
                if (currentGenerationNumber >= MaxGenerations)
                    return true;
            }
            */

            // we may need to sort the current generation by fitness, depending on SelectionStyle
            if (needToSortByFitness)
            {
                if (IsLowerFitnessBetter)
                    currentGeneration = currentGeneration.OrderBy(c => c.Fitness).ToList();
                else
                    currentGeneration = currentGeneration.OrderByDescending(c => c.Fitness).ToList();
            }

            // depending on the SelectionStyle, we may need to adjust all candidate's fitness scores
            AdjustFitnessScores(currentGeneration);

            // Start building the next generation
            var nextGeneration = new List<CandidateSolution<T,S>>();

            // Elitism
            var theBest = currentGeneration.Take(numElitesToAdd);
            foreach (var peakPerformer in theBest)
            {
                nextGeneration.Add(peakPerformer);
            }

            //前世代の個体をのこす
            for (int i = 0; i < currentGeneration.Count; i++)
            {
                if(currentGeneration[i].Fitness==1){
                    nextGeneration.Add(currentGeneration[i]);
                }
            }

            // now create a new generation using fitness scores for selection, and crossover and mutation
            while (nextGeneration.Count < PopulationSize)
            {
                // select parents
                CandidateSolution<T, S> parent1 = null, parent2 = null;
                switch(SelectionStyle)
                {
                    case SelectionStyle.Tourney:
                        parent1 = TournamentSelectParent();
                        parent2 = TournamentSelectParent();
                        break;

                    case SelectionStyle.RouletteWheel:
                    case SelectionStyle.Ranked:
                        parent1 = RouletteSelectParent();
                        parent2 = RouletteSelectParent();
                        break;
                }

                // cross them over to generate two new children
                CandidateSolution<T,S> child1, child2;
                CrossOverParents(parent1, parent2, out child1, out child2);

                // Mutation
                if (Randomizer.GetFloatFromZeroToOne() < MutationRate)
                    child1.Mutate();
                if (Randomizer.GetFloatFromZeroToOne() < MutationRate)
                    child2.Mutate();

                // then add to the new generation 
                nextGeneration.Add(child1);
                if(nextGeneration.Count < PopulationSize){
                    nextGeneration.Add(child2);
                }
                
            }

            currentGeneration = nextGeneration;
            currentGenerationNumber++;
            currentCandidateNumber = 0;

            return false;
        }

        private void AdjustFitnessScores(List<CandidateSolution<T, S>> currentGeneration)
        {
            // if doing ranked, adjust the fitness scores to be the ranking, with 0 = worst, (N-1) = best.
            // this style is good if the fitness scores for different candidates in the same generation would vary widely, especially
            // in early generations.  It smooths out those differences, which allows more genetic diversity
            if (SelectionStyle == SelectionStyle.Ranked)
            {
                float fitness = currentGeneration.Count - 1;
                foreach (var candidate in currentGeneration)
                    candidate.Fitness = fitness--;
            }

            // and calc total and highest fitness for two kinds of selections
            totalFitness = 0;
            float largestFitness = float.MinValue;
            if (SelectionStyle == SelectionStyle.RouletteWheel || SelectionStyle == SelectionStyle.Ranked)
            {
                foreach (var candidate in currentGeneration)
                {
                    float fitness = candidate.Fitness;
                    totalFitness += fitness;
                    if (fitness > largestFitness)
                        largestFitness = fitness;
                }
            }  

            // if it's roulette wheel, but lower fitness scores are better, adjust by subtract each fitness from the largest
            if (SelectionStyle == SelectionStyle.RouletteWheel && IsLowerFitnessBetter)
            {
                foreach (var candidate in currentGeneration)
                    candidate.Fitness = largestFitness - candidate.Fitness;
            }
        }

        private void CrossOverParents(
            CandidateSolution<T,S> parent1, CandidateSolution<T,S> parent2, 
            out CandidateSolution<T,S> child1, out CandidateSolution<T,S> child2)
        {
            // are we crossing over, or just copying a parent directly?
            child1 = parent1.Clone();
            child2 = parent2.Clone();

            // Do we use exact copies of parents, or crossover?
            if (Randomizer.GetDoubleFromZeroToOne() < CrossoverRate)
            {
                NodeBaseType<T, S> randomC1node, randomC2node;
                // pick a random node from both parents and clone the tree below it.
                // We don't want root nodes, since that's not really crossover, so we disallow those
                do
                {
                    randomC1node = child1.SelectRandomNode();
                } while (randomC1node == child1.Root);
                do
                {
                    randomC2node = child2.SelectRandomNode();
                } while (randomC2node == child2.Root);

                // make copies of them 
                var newChild1 = randomC1node.Clone(null);
                var newChild2 = randomC2node.Clone(null);

                // create new children by swapping subtrees
                CandidateSolution<T,S>.SwapSubtrees(randomC1node, randomC2node, newChild1, newChild2);
            }

            // and reset the pointers to the candidate
            child1.Root.SetCandidateRef(child1);
            child2.Root.SetCandidateRef(child2);
        }

        // Selection Routines -----------------------------------------------

        private CandidateSolution<T,S> TournamentSelectParent()
        {
            CandidateSolution<T,S> result = null;
            float bestFitness = float.MaxValue;
            if (IsLowerFitnessBetter == false)
                bestFitness = float.MinValue;

            for(int i = 0; i < TourneySize; i++)
            {
                int index = Randomizer.IntLessThan(PopulationSize);
                var randomTree = currentGeneration[index];
                var fitness = randomTree.Fitness;

                bool isFitnessBetter = false;
                if (IsLowerFitnessBetter)
                    isFitnessBetter = fitness < bestFitness;
                else
                    isFitnessBetter = fitness > bestFitness;
                if (isFitnessBetter)
                {
                    result = randomTree;
                    bestFitness = fitness;
                }
            }

            System.Diagnostics.Debug.Assert(result != null);
            return result;
        }

        private CandidateSolution<T, S> RouletteSelectParent()
        {
            // using Roulette Wheel Selection, we grab a possibility proportionate to it's fitness compared to
            // the total fitnesses of all possibilities
            double randomValue = Randomizer.GetDoubleFromZeroToOne() * totalFitness;
            for (int i = 0; i < PopulationSize; i++)
            {
                randomValue -= currentGeneration[i].Fitness;
                if (randomValue <= 0)
                {
                    return currentGeneration[i];
                }
            }

            return currentGeneration[PopulationSize - 1];
        }

        //--------------------------------------------------------------------------------

        public void AddConstant(T value)
        {
            primitiveSet.Constants.Add(value);
        }

        //--------------------------------------------------------------------------------

        public void AddVariable(string variableName)
        {
            if (!primitiveSet.VariableNames.Contains(variableName))
                primitiveSet.VariableNames.Add(variableName);
        }

        //--------------------------------------------------------------------------------

        // zero parameter functions are a type of terminal, so they are stored separate from the other functions
        public void AddTerminalFunction(Func<S, T> function, string functionName)
        {
            primitiveSet.TerminalFunctions.Add(new FunctionMetaData<T>(function, 0, functionName));
        }

        //--------------------------------------------------------------------------------

        // zero param functions not allowed
        // one parameter functions
        public void AddFunction(Func<T, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 1, functionName, false));
        }

        // two parameter functions
        public void AddFunction(Func<T, T, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 2, functionName));
        }

        // three parameter functions
        public void AddFunction(Func<T, T, T, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 3, functionName));
        }

        // four parameter functions
        public void AddFunction(Func<T, T, T, T, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 4, functionName));
        }

        //--------------------------------------------------------------------------------

        public void AddStatefulFunction(Func<T, S, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 1, functionName, true));
        }

        // two parameter functions
        public void AddStatefulFunction(Func<T, T, S, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 2, functionName, true));
        }

        // three parameter functions
        public void AddStatefulFunction(Func<T, T, T, S, T> function, string functionName)
        {
            primitiveSet.Functions.Add(new FunctionMetaData<T>(function, 3, functionName, true));
        }

        //--------------------------------------------------------------------------------

        // And a reference to the fitness function
        public void AddFitnessFunction(Func<CandidateSolution<T,S>,float> fitnessFunction)
        {
            myFitnessFunction = new FitnessFunctionPointer(fitnessFunction);
        }

        public void AddScanTreeFunction(Func<CandidateSolution<T,S>, dynamic, T> scanTreeFunction)
        {
            myScanTreeFunc = new ScanTreeFuncPointer(scanTreeFunction);
        }

        //--------------------------------------------------------------------------------

        // and allow the caller to monitor progress, and even halt the engine
        public void AddProgressFunction(Func<EngineProgress, bool> progressFunction)
        {
            myProgressFunction = new ProgressFunctionPointer(progressFunction);
        }

        public void AddProgressFunction(Func<EngineProgress, CandidateSolution<T, S>, bool> progressFunction)
        {
            myProgressWithCandidateFunction = new ProgressWithCandidateFunctionPointer(progressFunction);
        }
    }
}
