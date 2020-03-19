﻿//
// Copyright (c) Charles Simon. All rights reserved.  
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
//  

using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using BrainSimulator.Modules;

namespace BrainSimulator
{
    public partial class NeuronArray
    {

#if DEBUG
        public int arraySize = 10000; // ten thousand neurons to start
        public int rows = 100;
#else
        public int arraySize = 1000000; // a million neurons to start
        public int rows = 1000;
#endif
        internal List<ModuleView> modules = new List<ModuleView>();
        public List<ModuleView> Modules
        {
            get { return modules; }
        }

        public Neuron[] neuronArray;
        public long Generation { get; set; } = 0;

        public DisplayParams displayParams;

        //notes about this network
        public string networkNotes = "Purpose:\n\r\n\rThings to try:\n\r\n\rCurrent state of development:\n\r\n\rNotes:\n\r\n\r";
        public bool hideNotes = false;

        //this has nothing to do with the NeuronArray but is here so it will be saved and restored with the network
        private int engineSpeed = 250;
        public int EngineSpeed { get => engineSpeed; set => engineSpeed = value; }

        //keeps track of the number of neurons which fired in this generation
        [XmlIgnore]
        public long fireCount = 0;
        [XmlIgnore]
        public long lastFireCount = 0;

        //this list keeps track of changing synapses
        struct SynapseUndo
        {
            public int source, target;
            public float weight;
            public bool newSynapse;
        }
        List<SynapseUndo> synapseUndoInfo = new List<SynapseUndo>();
        
        public NeuronArray()
        {
            neuronArray = new Neuron[arraySize];
            Parallel.For(0, arraySize, i => neuronArray[i] = new Neuron(i));

            //for (int i = 0; i < neuronArray.Length; i++)
            //{
            //    Neuron n = new Neuron(i);
            //    neuronArray[i] = n;
            //}
        }

        public NeuronArray(int theSize, int theRows, Neuron.modelType t = Neuron.modelType.Std)
        {
            arraySize = theSize;
            rows = theRows;

            neuronArray = new Neuron[arraySize];
            Parallel.For(0, arraySize, i => neuronArray[i] = new Neuron(i,t)) ;
        }

 
        public void Fire()
        {
            HandleProgrammedActions();
            lastFireCount = fireCount;
            fireCount = 0;

            //when debugging the Fire1 & Fire2 modules disable parallel operation and use the sequential loops below;
            Parallel.For(0, arraySize, i => neuronArray[i].Fire1(this,Generation));
            Parallel.For(0, arraySize, i => neuronArray[i].Fire2(Generation));

            //use these instead
            //foreach (Neuron n in neuronArray)
            //    n.Fire1(this,Generation);
            //foreach (Neuron n in neuronArray)
            //    n.Fire2(Generation);
            Generation++;
        }

        //fires all the modules
        private void HandleProgrammedActions()
        {
            //lock (modules)
            {
                for (int i = 0; i < modules.Count; i++)// each (ModuleView na in modules)
                {
                    ModuleView na = modules[i];
                    if (na.TheModule != null)
                    {
                        na.TheModule.Fire();
                    }
                }
            }
        }

        //looks for a beginning match only
        private ModuleView FindAreaByCommand(string command)
        {
            return modules.Find(na => na.CommandLine.IndexOf(command) == 0);
        }

        //needs a complete match
        public ModuleView FindAreaByLabel(string label)
        {
            return modules.Find(na => na.Label.Trim() == label);
        }


        public void AddSynapseUndo(int source, int target, float weight, bool newSynapse)
        {
            SynapseUndo s;
            s = new SynapseUndo
            {
                source = source,
                target = target,
                weight = weight,
                newSynapse = newSynapse
            };
            synapseUndoInfo.Add(s);
        }
        public void UndoSynapse()
        {
            if (synapseUndoInfo.Count == 0) return;
            SynapseUndo s = synapseUndoInfo.Last();
            synapseUndoInfo.Remove(s);
            Neuron n = neuronArray[s.source];
            if (s.newSynapse)
            {
                n.DeleteSynapse(s.target);
            }
            else
            {
                n.AddSynapse(s.target, s.weight, this);
                synapseUndoInfo.RemoveAt(synapseUndoInfo.Count - 1);
            }
        }

        public void CheckSynapseArray()
        {
            for (int i = 0; i < neuronArray.Length; i++)
            {
                Neuron n = neuronArray[i];
                n.Id = i;
                for (int j = 0; j < n.synapses.Count; j++)
                {
                    Synapse s = n.synapses[j];
                    if (s.TargetNeuron == -1)
                    {
                        n.synapses.RemoveAt(j);
                        j--;
                    }
                }
                for (int j = 0; j < n.synapsesFrom.Count; j++)
                {
                    Synapse s = n.synapsesFrom[j];
                    if (s.TargetNeuron == -1)
                    {
                        n.synapsesFrom.RemoveAt(j);
                        j--;
                    }
                }
            }
            for (int i = 0; i < neuronArray.Length; i++)
            {
                Neuron n = neuronArray[i];
                foreach (Synapse s in n.Synapses)
                {
                    Neuron nTarget = neuronArray[s.TargetNeuron];
                    Synapse s1 = nTarget.SynapsesFrom.Find(s2 => s2.TargetNeuron == i);
                    if (s1 == null)
                        nTarget.SynapsesFrom.Add(new Synapse(i, s.Weight));
                }
            }
        }

        public int GetNeuronIndex(int x, int y)
        {
            return x * rows + y;
        }

        public void GetNeuronLocation(int index, out int x, out int y)
        {
            x = index / rows;
            y = index % rows;
        }


    }
}
