using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RLManager : MonoBehaviour
{
    public static int mode = 0; // 0: solver, 1: generator

    public GameObject levelGenerator;
    public GameObject levelSolver;

    private int generatorStep = 1000;
    private int solverStep = 100;

    public static LevelGenerator generatorCopy;
    public static LevelSolver solverCopy;
    public static int currentGeneratorStep = 0;
    public static int currentSolverStep = 0;

    // Start is called before the first frame update
    void Start()
    {
        LevelGenerator generator = Instantiate(levelGenerator).GetComponent<LevelGenerator>();
        LevelSolver solver = Instantiate(levelSolver).GetComponent<LevelSolver>();

        generatorCopy = generator;
        solverCopy = solver;
        print("solver learning start");
    }

    // Update is called once per frame
    void Update()
    {
        if(mode == 0)
        {
            if(currentSolverStep > solverStep)
            {
                currentSolverStep = 0;
                mode = 1;
                print("generator learning start");
            }
        }
        else if(mode == 1)
        {
            if (currentGeneratorStep > generatorStep)
            {
                currentGeneratorStep = 0;
                mode = 0;
                print("solver learning start");
            }
        }
    }
}
