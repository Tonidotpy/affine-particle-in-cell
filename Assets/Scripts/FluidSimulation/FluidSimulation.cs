using UnityEngine;

public class FluidSimulation : MonoBehaviour
{
    [Header("SimulationType")]
    [Tooltip("Check for GPU Simulation, uncheck for CPU Simulation")]
    public bool useGPUSimulation = true;

    [Header("Simulation References")]
    public GameObject cpuSimulationObject;
    public GameObject gpuSimulationObject;

    private void Awake()
    {
        UpdateSimulationState();
    }

    private void UpdateSimulationState()
    {
        if (cpuSimulationObject != null)
        {
            cpuSimulationObject.SetActive(!useGPUSimulation);
        }
        if (gpuSimulationObject != null)
        {
            gpuSimulationObject.SetActive(useGPUSimulation);
        }
    }

    // Update is called once per frame
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }
                UpdateSimulationState();
            };
        }
    }
}
