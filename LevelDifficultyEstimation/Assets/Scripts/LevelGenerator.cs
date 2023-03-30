using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public class LevelGenerator : Agent
{
    public GameObject floorObject;
    public GameObject startObject;
    public GameObject endObject;

    public float minDistance = 5;
    public float maxDistance = 10;
    public float minAngle = -180;
    public float maxAngle = 180;
    public float minScale = 4;
    public float maxScale = 6;
    public float minHeight = -2;
    public float maxHeight = 2;

    public float minWorld = 0;
    public float maxWorld = 50;

    public float maxChange = 50;

    public static bool isGenerateAble = false;

    public Vector3 startPosition;
    public Vector3 endPosition;

    // private 

    public GameObject latestObject;
    public GameObject latestObject2;
    public GameObject targetObject;

    private float currentChange = 0;
    private float yScale = 0.5f;

    float ReturnNewRange(float value, float newMin, float newMax)
    {
        float oldMin = -1;
        float oldMax = 1;

        float newValue = (value - oldMin) / (oldMax - oldMin) * (newMax - newMin) + newMin;
        return newValue;
    }

    void ReturnReward()
    {
        float normalizedDistance = Vector3.Distance(latestObject.transform.position, targetObject.transform.position) / Vector3.Distance(startPosition, endPosition);
        float extReward = 0;
        float intReward = -1 * Mathf.Log(normalizedDistance);

        SetReward(intReward + extReward);
    }
    public override void OnEpisodeBegin()
    {
        // 삭제할 태그
        string tagToDelete = "level";

        // 모든 태그가 붙은 게임 오브젝트를 찾아서 삭제
        foreach (GameObject obj in GameObject.FindGameObjectsWithTag(tagToDelete))
        {
            Destroy(obj);
        }

        startPosition = new Vector3(Random.Range(minWorld, maxWorld), 0, Random.Range(minWorld, maxWorld));
        endPosition = new Vector3(Random.Range(minWorld, maxWorld), 20, Random.Range(minWorld, maxWorld));

        Vector3 v = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * Random.Range(minDistance, maxDistance) + Vector3.up * Random.Range(minHeight, maxHeight);
        latestObject = Instantiate(floorObject, startPosition + v, Quaternion.identity);
        latestObject2 = Instantiate(startObject, startPosition, Quaternion.identity);
        targetObject = Instantiate(endObject, endPosition, Quaternion.identity);

        float scale = Random.Range(minScale, maxScale);
        latestObject.transform.localScale = new Vector3(scale, yScale, scale);
        scale = Random.Range(minScale, maxScale);
        latestObject2.transform.localScale = new Vector3(scale, yScale, scale);
        scale = Random.Range(minScale, maxScale);
        targetObject.transform.localScale = new Vector3(scale, yScale, scale);

        currentChange = 0;

        RLManager.solverCopy.transform.position = startPosition + new Vector3(0, 1, 0);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(targetObject.transform.position));
/*        sensor.AddObservation(transform.InverseTransformDirection(targetObject.transform.eulerAngles));*/
        sensor.AddObservation(Vector3.Distance(latestObject.transform.position, targetObject.transform.position));
        sensor.AddObservation(transform.InverseTransformPoint(latestObject.transform.position));
        /*        sensor.AddObservation(transform.InverseTransformDirection(latestObject.transform.eulerAngles));*/
        sensor.AddObservation(latestObject.transform.lossyScale);
    }


    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (isGenerateAble)
        {
            currentChange += 1;

            var continuousActions = actionBuffers.ContinuousActions;
            float distance = ReturnNewRange(continuousActions[0], minDistance, maxDistance);
            float angle = ReturnNewRange(continuousActions[1], minAngle, maxAngle);
            float scale = ReturnNewRange(continuousActions[2], minScale, maxScale);
            float height = ReturnNewRange(continuousActions[3], minHeight, maxHeight);

            Vector3 v = (latestObject2.transform.position - latestObject.transform.position).normalized;
            Vector3 vec = Quaternion.Euler(0, angle, 0) * new Vector3(v.x, 0, v.z);
            vec *= distance;
            Vector3 newPosition = latestObject.transform.position + vec + new Vector3(0, height, 0);
            GameObject newObject = Instantiate(floorObject, newPosition, Quaternion.identity);
            newObject.transform.localScale = new Vector3(scale, yScale, scale);

            latestObject2 = latestObject;
            latestObject = newObject;

            isGenerateAble = false;
        }

        if (RLManager.mode == 1)
        {
            ReturnReward();
        }

        float normalizedDistance = Vector3.Distance(latestObject.transform.position, targetObject.transform.position) / Vector3.Distance(startPosition, endPosition);
        if (currentChange > maxChange || normalizedDistance < 0.01f)
        {
            if (RLManager.mode == 1)
            {
                RLManager.currentGeneratorStep += 1;
            }
            EndEpisode();
        }
    }
}
