using Godot;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

public class NN
{
    public Layer[] layers;
    public int[] NNshape;

    public float MutationAmt;
    public float MutationChance;

    public class Layer
    {
        public float[,] weights;
        public float[] biases;
        public float[] outputs;

        public int ninp;
        public int nnodes;

        public Layer(int inputs, int nodes)
        {
            this.ninp = inputs;
            this.nnodes = nodes;

            weights = new float[inputs, nodes];
            biases = new float[nodes];
            outputs = new float[nodes];

            for (int n = 0; n < nodes; n++)
            {
                for (int i = 0; i < inputs; i++)
                {
                    this.weights[i, n] = RandomGaussian(0, .1f);
                }
                this.biases[n] = 0;
            }
        }

        public void forward(float[] inputs)
        {
            if(inputs.Length != ninp)
            {
                GD.Print("Input Shape Mismatch");
            }
            for (int i = 0; i < nnodes; i++)
            {
                float res = 0;
                for (int j = 0; j < ninp; j++)
                {
                    res += inputs[j] * weights[j, i];
                }
                res /= Mathf.Sqrt(ninp);
                res += biases[i];
                outputs[i] = res;
            }
        }

        public void sterilize(float[] inputs)
        {
            if (inputs.Length != ninp)
            {
                GD.Print("Input Shape Mismatch");
            }
            for (int i = 0; i < nnodes; i++)
            {
                for (int j = 0; j < ninp; j++)
                {
                    if(inputs[j] == 1)
                    {
                        weights[j, i] = RandomGaussian(0, .01f);
                    }
                }
            }
        }

        public void relu()
        {
            for (int i = 0; i < nnodes; i++)
            {
                if (outputs[i] < 0)
                {
                    outputs[i] = outputs[i] * 0.01f;
                }
            }
        }

        public void tanSigmoid()
        {
            for (int i = 0; i < nnodes; i++)
            {
                float x = Mathf.Clamp(outputs[i], -20f, 20f);
                outputs[i] = ((1f / (1f + Mathf.Exp(-x))) * 2f) - 1f;
            }
        }

        public void softSign()
        {
            for (int i = 0; i < nnodes; i++)
            {
                float x = outputs[i] * 3f;
                outputs[i] = x / (1 + Mathf.Abs(x));
            }
        }
    }

    public NN(int[] nnShape, float mutationAmt, float mutationChance)
    {
        NNshape = nnShape;
        MutationAmt = mutationAmt;
        MutationChance = mutationChance;

        layers = new Layer[NNshape.Length - 1];
        for (int i = 0; i < NNshape.Length - 1; i++)
        {
            layers[i] = new Layer(NNshape[i], NNshape[i + 1]);
        }
    }

    public NN()
    {
        layers = new Layer[NNshape.Length - 1];
        for (int i = 0; i < NNshape.Length - 1; i++)
        {
            layers[i] = new Layer(NNshape[i], NNshape[i + 1]);
        }
    }

    public float[] forward(float[] inputs)
    {
        int nnlen = NNshape.Length - 2;

        layers[0].forward(inputs);

        if (nnlen > 0)
        {
            layers[0].softSign();
            for (int i = 1; i < nnlen; i++)
            {
                layers[i].forward(layers[i - 1].outputs);
                layers[i].softSign();
            }
            layers[nnlen].forward(layers[nnlen - 1].outputs);
        }
        layers[nnlen].softSign();
        return layers[nnlen].outputs;
    }

    public Layer[] makeCopy()
    {
        Layer[] templayers = new Layer[NNshape.Length - 1];

        for (int i = 0; i < NNshape.Length - 1; i++)
        {
            templayers[i] = new Layer(NNshape[i], NNshape[i + 1]);
            templayers[i].weights = (float[,])layers[i].weights.Clone();
            templayers[i].biases = (float[])layers[i].biases.Clone();
        }
        return templayers;
    }

    public Layer[] reproduce(Layer[] spouce)
    {
        Layer[] templayers = new Layer[NNshape.Length - 1];

        for (int i = 0; i < NNshape.Length - 1; i++)
        {
            templayers[i] = new Layer(NNshape[i], NNshape[i + 1]);

            for (int n = 0; n < NNshape[i + 1]; n++)
            {
                for (int j = 0; j < NNshape[i]; j++)
                {
                    if (GD.RandRange(0f, 100f) > 50f)
                    {
                        templayers[i].weights[j, n] = layers[i].weights[j, n];
                    }
                    else
                    {
                        templayers[i].weights[j, n] = spouce[i].weights[j, n];
                    }

                }
                if (GD.RandRange(0f, 100f) > 50f)
                {
                    templayers[i].biases[n] = layers[i].biases[n];
                }
                else
                {
                    templayers[i].biases[n] = spouce[i].biases[n];
                }
            }

        }
        return templayers;
    }

    public void mutate()
    {
        mutateLearningRate(.05f, 5f);

        for (int i = 0; i < NNshape.Length - 1; i++)
        {
            var distFromOutputLayer = (NNshape.Length - 1) - (i + 1);
            var layerMutationAmt = MutationAmt * Mathf.Pow(.9f, distFromOutputLayer);
            var layerMutationChance = MutationChance * Mathf.Pow(.9f, distFromOutputLayer);

            for (int j = 0; j < NNshape[i + 1]; j++)
            {
                for (int k = 0; k < NNshape[i]; k++)
                {
                    
                    if (GD.RandRange(0f, 100f) < layerMutationChance)
                    {
                        layers[i].weights[k, j] += RandomGaussian(0, layerMutationAmt);
                        layers[i].weights[k, j] = Mathf.Clamp(layers[i].weights[k, j], -10f, 10f);
                    }
                }
                if (GD.RandRange(0f, 100f) < layerMutationChance)
                {
                    layers[i].biases[j] += RandomGaussian(0, (layerMutationAmt / 30));
                    layers[i].biases[j] = Mathf.Clamp(layers[i].biases[j], -10f, 10f);
                }
            }
        }
    }

    public void mutateLearningRate(float mutateAmt, float mutateChance)
    {
        MutationAmt += WorldParameters.RandomNP() * mutateAmt;
        MutationAmt = Mathf.Clamp(MutationAmt, .05f, 1f);
        MutationChance += WorldParameters.RandomNP() * mutateChance;
        MutationChance = Mathf.Clamp(MutationChance, 10f, 100f);
    }

    public NN copyWithMutation()
    {
        NN newBrain = this.clone();
        newBrain.mutate();
        return newBrain;
    }

    public NN clone()
    {
        NN newBrain = new NN(NNshape, MutationAmt, MutationChance);
        newBrain.layers = this.makeCopy();

        return newBrain;
    }

    //Load & Save
    public void save(string path, int verbose = 0)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // Create a StreamWriter to write to the specified file
        using (StreamWriter writer = new StreamWriter(path))
        {
            // Save the shape of the neural network
            writer.WriteLine(string.Join(",", NNshape));

            // Save each layer
            foreach (var layer in layers)
            {
                // Save layer details
                writer.WriteLine(layer.ninp + "," + layer.nnodes);

                // Save weights
                for (int i = 0; i < layer.ninp; i++)
                {
                    for (int j = 0; j < layer.nnodes; j++)
                    {
                        writer.Write(layer.weights[i, j]);
                        if (j < layer.nnodes - 1)
                            writer.Write(",");
                    }
                    writer.WriteLine();
                }

                // Save biases
                for (int i = 0; i < layer.nnodes; i++)
                {
                    writer.Write(layer.biases[i].ToString(CultureInfo.InvariantCulture));
                    if (i < layer.nnodes - 1)
                        writer.Write(",");
                }
                writer.WriteLine();
            }
        }

        if (verbose > 0)
            GD.Print("Neural network saved to: " + path);
    }

    public void load(string path, int verbose = 0)
    {
        // Create a StreamReader to read the specified file
        using (StreamReader reader = new StreamReader(path))
        {
            // Read the neural network shape
            string[] shapeString = reader.ReadLine().Split(',');
            int[] loadedShape = new int[shapeString.Length];
            for (int i = 0; i < shapeString.Length; i++)
            {
                loadedShape[i] = int.Parse(shapeString[i]);
            }

            // Validate and set the shape
            if (loadedShape.Length != NNshape.Length)
            {
                GD.Print("Mismatched neural network shapes. Cannot load.");
                return;
            }

            NNshape = loadedShape;

            // Read each layer
            for (int layerIndex = 0; layerIndex < NNshape.Length - 1; layerIndex++)
            {
                // Read layer details
                string[] layerDetails = reader.ReadLine().Split(',');
                int ninp = int.Parse(layerDetails[0]);
                int nnodes = int.Parse(layerDetails[1]);

                // Initialize the layer
                layers[layerIndex] = new Layer(ninp, nnodes);

                // Read weights
                for (int i = 0; i < ninp; i++)
                {
                    string[] weightsString = reader.ReadLine().Split(',');
                    for (int j = 0; j < nnodes; j++)
                    {
                        layers[layerIndex].weights[i, j] = float.Parse(weightsString[j], CultureInfo.InvariantCulture);
                    }
                }

                // Read biases
                string[] biasesString = reader.ReadLine().Split(',');
                for (int i = 0; i < nnodes; i++)
                {
                    layers[layerIndex].biases[i] = float.Parse(biasesString[i], CultureInfo.InvariantCulture);
                }
            }
        }
        if(verbose > 0)
            GD.Print("Neural network loaded from: " + path);
    }

    public static class NNHelper
    {
        public static NN MakeOffspring(NN p1, NN p2)
        {
            NN child = new NN(p1.NNshape, p1.MutationAmt, p1.MutationChance);
            child.layers = p1.reproduce(p2.layers);

            return child;
        }

        public static NN MakeOffspring(NN p1)
        {
            NN child = new NN(p1.NNshape, p1.MutationAmt, p1.MutationChance);
            child.layers = p1.makeCopy();
            child.mutate();

            return child;
        }
    }

    public static float RandomGaussian(float mean = 0f, float stdDev = 1f)
    {
        float u1 = Mathf.Max(1e-7f, 1f - GD.Randf());
        float u2 = 1f - GD.Randf();

        float randStdNormal =
            Mathf.Sqrt(-2f * Mathf.Log(u1)) *
            Mathf.Sin(2f * Mathf.Pi * u2);

        return mean + stdDev * randStdNormal;
    }

    public static NN LoadSavedNN(string path)
    {
        int[] loadedShape;
        using (StreamReader reader = new StreamReader(path))
        {
            // Read the neural network shape
            string[] shapeString = reader.ReadLine().Split(',');
            loadedShape = new int[shapeString.Length];
            for (int i = 0; i < shapeString.Length; i++)
            {
                loadedShape[i] = int.Parse(shapeString[i]);
            }
        }
        
        var nn = new NN(loadedShape, 1, 1);
        nn.load(path);
        return nn;
    }

    public static NN Extend(NN currNN, int[] newShape, float initWeights = 0.01f)
    {
        var currShape = currNN.NNshape;
        var extendedNN = new NN(newShape, currNN.MutationAmt, currNN.MutationChance);

        for(int l = 0; l < extendedNN.layers.Length; l++)
        {
            var layer = extendedNN.layers[l];
            var currNNLayer = currNN.layers[l];

            for (int n = 0; n < layer.nnodes; n++)
            {
                for (int i = 0; i < layer.ninp; i++)
                {
                    if (n < currNNLayer.nnodes && i < currNNLayer.ninp)
                        layer.weights[i, n] = currNNLayer.weights[i, n];
                    else
                        layer.weights[i, n] = RandomGaussian(0, initWeights);
                }
                if (n < currNNLayer.nnodes)
                    layer.biases[n] = currNNLayer.biases[n];
                else
                    layer.biases[n] = 0;
            }
        }

        return extendedNN;
    }

    public static NN ExtendNumPlayers(NN brain, int currTeamSize, int addPlayers)
    {
        var brainShape = brain.NNshape.ToArray();
        brainShape[0] += addPlayers * TrainingParameters.InputsPerPlayer * 2;
        var newBrain = Extend(brain, brainShape);

        var teamMatesStartIndex = GetOpponentIndex(0, currTeamSize);
        var teamMatesEndIndex = GetEndIndex(teamMatesStartIndex, currTeamSize-1);
        CopyInputsBlock(newBrain, teamMatesStartIndex, teamMatesEndIndex, addPlayers * TrainingParameters.InputsPerPlayer);

        for(int i = 0; i < addPlayers; i++)
        {
            var SelectOpp = currTeamSize - 1;
            var StartIndex = GetTeammateIndex(SelectOpp);
            var EndIndex = GetEndIndex(StartIndex, 1);
            CopyInputsBlock(newBrain, StartIndex, EndIndex,  (currTeamSize - SelectOpp + i) * TrainingParameters.InputsPerPlayer);
        }

        for (int i = 0; i < addPlayers; i++)
        {
            var SelectTeammate = currTeamSize - 2;
            var StartIndex = GetOpponentIndex(SelectTeammate, currTeamSize);
            var EndIndex = GetEndIndex(StartIndex, 1);
            CopyInputsBlock(newBrain, StartIndex, EndIndex, (currTeamSize - 1 - SelectTeammate + i) * TrainingParameters.InputsPerPlayer);
        }

        return newBrain;
    }

    private static int GetOpponentIndex(int i, int teamSize)
    {
        return TrainingParameters.ExtraParams + TrainingParameters.NNShapeS2[0] + TrainingParameters.MemorySize + (TrainingParameters.InputsPerPlayer * i) + (TrainingParameters.InputsPerPlayer * teamSize);
    }

    private static int GetTeammateIndex(int i)
    {
        return TrainingParameters.ExtraParams + TrainingParameters.NNShapeS2[0] + TrainingParameters.MemorySize + (TrainingParameters.InputsPerPlayer * i);
    }

    private static int GetEndIndex(int start, int numPlayers)
    {
        return start + numPlayers * TrainingParameters.InputsPerPlayer - 1;
    }

    private static void CopyInputsBlock(NN brain, int startInd, int endInd, int moveTo)
    {
        var nextLayer = brain.NNshape[1];
        var layer = brain.layers[0].weights;
        for(int i = startInd; i<= endInd; i++)
        {
            for (int j = 0; j < nextLayer; j++)
            {
                layer[i + moveTo, j] = layer[i, j];
            }
        }
    }
}


