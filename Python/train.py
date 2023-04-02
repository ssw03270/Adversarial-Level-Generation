from mlagents_envs.environment import UnityEnvironment as UE
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel

from tools.tools import get_observation_size, get_observation, get_action_size
from tools.PPO import PPO

import numpy as np
import torch
from tqdm import tqdm

def train(env, train_agent, test_agent):
    env.reset()
    done = False
    total_reward = 0

    while not done:
        test(env, test_agent)

        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        observation = np.array(get_observation(decision_steps.obs)).astype(np.float32)
        prob = train_agent['model'].pi(torch.from_numpy(observation)).detach().numpy()
        action_matrix = prob + np.random.normal(loc=0, scale=1.0, size=prob.shape)
        action = np.clip(action_matrix, -1, 1).reshape((1, -1))
        env.set_actions(train_agent['name'], ActionTuple(continuous=action))

        env.step()

        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        done = len(terminal_steps.obs[0]) != 0

        if not done:
            reward = decision_steps.reward[0]
            next_observation = np.array(get_observation(decision_steps.obs))
        else:
            reward = terminal_steps.reward[0]
            next_observation = np.array(get_observation(terminal_steps.obs))
        total_reward += reward

        train_agent['model'].put_data((observation, action, reward, next_observation, prob, done))

    train_agent['model'].train_net()
    return total_reward

def test(env, agent):
    decision_steps, terminal_steps = env.get_steps(agent['name'])
    observation = np.array(get_observation(decision_steps.obs))
    prob = agent['model'].pi(torch.from_numpy(observation.astype(np.float32))).detach().numpy()
    action_matrix = prob + np.random.normal(
        loc=0, scale=1.0, size=prob.shape
    )
    action = np.clip(action_matrix, -1, 1).reshape((1, -1))
    env.set_actions(agent['name'], ActionTuple(continuous=action))

if __name__ == '__main__':
    file_path = 'Build/LevelDifficultyEstimation.exe'
    config_channel = EngineConfigurationChannel()
    config_channel.set_configuration_parameters(
        width=900, height=450, time_scale=50.0
    )
    env = UE(file_name=file_path, seed=1, side_channels=[config_channel])
    env.reset()

    behavior_names = list(env.behavior_specs)
    generator_name = behavior_names[0]
    solver_name = behavior_names[1]
    print(f"Name of the generator behavior: {generator_name}")
    print(f"Name of the solver behavior: {solver_name}")

    generator_spec = env.behavior_specs[generator_name]
    solver_spec = env.behavior_specs[solver_name]
    print(f"Number of the generator observations: {generator_spec.observation_specs}")
    print(f"Number of the solver observations: {solver_spec.observation_specs}")

    generator_obs_size = get_observation_size(generator_spec.observation_specs)
    solver_obs_size = get_observation_size(solver_spec.observation_specs)

    generator_act_size = get_action_size(generator_spec.action_spec)
    solver_act_size = get_action_size(solver_spec.action_spec)

    generator_model = PPO(obs_size=generator_obs_size, act_size=generator_act_size)
    solver_model = PPO(obs_size=solver_obs_size, act_size=solver_act_size)

    generator = {'name': generator_name, 'spec': generator_spec, 'model': generator_model}
    solver = {'name': solver_name, 'spec': solver_spec, 'model': solver_model}

    is_solver_turn = True
    episode_num = 100000
    solver_max = 100
    generator_max = 1000
    solver_current = 0
    generator_current = 0
    total_reward = []
    for episode in tqdm(range(1, episode_num + 1)):
        if is_solver_turn:
            total_reward.append(train(env, solver, generator))
            train_agent = solver
            solver_current += 1
        else:
            total_reward.append(train(env, generator, solver))
            train_agent = generator
            generator_current += 1

        if solver_current == solver_max or generator_current == generator_max:
            is_solver_turn = not is_solver_turn
            mean = np.array(total_reward).mean()
            std = np.array(total_reward).std()
            total_reward = []
            solver_current = generator_current = 0
            print(f"{train_agent['name']}: Episode {episode}, Mean Reward {mean:.2f}, Std Reward {std:.2f}")

    torch.save(generator_model.state_dict(), "./generator.h5")
    torch.save(solver_model.state_dict(), "./solver.h5")

    env.close()