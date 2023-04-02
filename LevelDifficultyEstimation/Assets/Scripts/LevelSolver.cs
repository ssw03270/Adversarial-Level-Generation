using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

public class LevelSolver : Agent
{
    public LevelGenerator levelGenerator;

    public float moveSpeed = 5f;
    public float turnSpeed = 150f;
    public float jumpForce = 5f;
    private Rigidbody rb;

    private bool jumpAble = false;

    void ReturnReward(float additionalReward)
    {
        float normalizedDistance = Vector3.Distance(transform.position, levelGenerator.endPosition) / 
            Vector3.Distance(levelGenerator.startPosition, levelGenerator.endPosition);
        float intReward = Mathf.Exp(-3 * normalizedDistance);

        SetReward(intReward + additionalReward);
    }
    public override void OnEpisodeBegin()
    {
        rb = GetComponent<Rigidbody>();
        transform.position = levelGenerator.startPosition + new Vector3(0, 1, 0);
    }
    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.InverseTransformPoint(levelGenerator.endPosition));
        sensor.AddObservation(transform.InverseTransformPoint(levelGenerator.latestObject.transform.position));
        sensor.AddObservation(transform.InverseTransformDirection(rb.angularVelocity));
        sensor.AddObservation(transform.InverseTransformDirection(rb.velocity));
        sensor.AddObservation(rb.rotation);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var continuousActions = actionBuffers.ContinuousActions;

        float forward = continuousActions[0];

        Vector3 movement = new Vector3(0, 0, forward) * moveSpeed * Time.fixedDeltaTime;
        transform.Translate(movement);

        // 캐릭터 회전
        float turn = continuousActions[1];
        Quaternion turnRotation = Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);

        // 점프
        float jump = continuousActions[2];
        if (jump >=0 && jumpAble)
        {
            jumpAble = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        float normalizedDistance = Vector3.Distance(transform.position, levelGenerator.endPosition) / Vector3.Distance(levelGenerator.startPosition, levelGenerator.endPosition);
        if (transform.position.y < 0)
        {
            levelGenerator.ReturnReward(-1);
            ReturnReward(-1);
            levelGenerator.EndEpisode();
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        continuousActions[0] = Input.GetAxis("Vertical");
        continuousActions[1] = Input.GetAxis("Horizontal");
        if (Input.GetKey(KeyCode.Space))
        {
            continuousActions[2] = 1;
        }
        else
        {
            continuousActions[2] = -1;
        }

    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("floor") && transform.position.y > collision.gameObject.transform.position.y)
        {
            collision.gameObject.tag = "touchedFloor";
            LevelGenerator.isGenerateAble = true;
            ReturnReward(100);
        }
        if (collision.gameObject.CompareTag("touchedFloor") && transform.position.y > collision.gameObject.transform.position.y)
        {
            jumpAble = true;
        }
        if (collision.gameObject.CompareTag("endFloor") && transform.position.y > collision.gameObject.transform.position.y)
        {
            levelGenerator.ReturnReward(100);
            ReturnReward(100);
            levelGenerator.EndEpisode();
            EndEpisode();
        }
    }
}
