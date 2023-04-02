from mlagents_envs.environment import UnityEnvironment as UE
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.base_env import ActionTuple

import torch
import numpy as np
from collections import deque
import time
# import imageio
from torch.utils.tensorboard import SummaryWriter
from tqdm import tqdm

from tools.PPO import PPO, MemoryBuffer
from tools.tools import get_observation_size, get_observation, get_action_size

n_episodes = 40000
update_interval = 16000
log_interval = 10

def train(env, train_agent, test_agent, memory, time_step):
    env.reset()
    done = False
    total_reward = 0

    while not done:
        test(env, test_agent)

        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        state = np.array(get_observation(decision_steps.obs))

        action, log_prob = train_agent['model'].select_action(state)

        memory.states.append(state)
        memory.actions.append(action.detach().numpy())
        memory.logprobs.append(log_prob)

        env.set_actions(train_agent['name'], ActionTuple(continuous=np.array([action.data.numpy()])))
        env.step()

        decision_steps, terminal_steps = env.get_steps(train_agent['name'])
        done = len(terminal_steps.obs[0]) != 0

        if not done:
            reward = decision_steps.reward[0]
        else:
            reward = terminal_steps.reward[0]

        total_reward += reward

        memory.rewards.append(reward)
        memory.dones.append(done)

        time_step += 1
        if time_step % update_interval == 0:
            train_agent['model'].update(memory)
            time_step = 0
            memory.clear_buffer()

    return total_reward, time_step

def test(env, agent):
    decision_steps, terminal_steps = env.get_steps(agent['name'])
    state = np.array(get_observation(decision_steps.obs))

    action, log_prob = agent['model'].select_action(state)

    env.set_actions(agent['name'], ActionTuple(continuous=np.array([action.data.numpy()])))

def main():
    file_path = 'Build/LevelDifficultyEstimation.exe'
    config_channel = EngineConfigurationChannel()
    config_channel.set_configuration_parameters(
        width=900, height=450, time_scale=100.0
    )
    env = UE(file_name=file_path, seed=1, side_channels=[config_channel], no_graphics=True)
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

    generator_memory = MemoryBuffer()
    solver_memory = MemoryBuffer()
    generator_model = PPO(state_size=generator_obs_size, action_size=generator_act_size)
    solver_model = PPO(state_size=solver_obs_size, action_size=solver_act_size)

    generator = {'name': generator_name, 'spec': generator_spec, 'model': generator_model}
    solver = {'name': solver_name, 'spec': solver_spec, 'model': solver_model}

    is_solver_turn = True
    solver_max = 100
    generator_max = 1000
    solver_current = 0
    generator_current = 0
    solver_time_step = 0
    generator_time_step = 0
    total_reward = []
    for episode in tqdm(range(1, n_episodes + 1)):
        if is_solver_turn:
            reward, solver_time_step = train(env, solver, generator, solver_memory, solver_time_step)
            total_reward.append(reward)
            train_agent = solver
            solver_current += 1
        else:
            reward, generator_time_step = train(env, generator, solver, generator_memory, generator_time_step)
            total_reward.append(reward)
            train_agent = generator
            generator_current += 1

        if solver_current == solver_max or generator_current == generator_max:
            is_solver_turn = not is_solver_turn
            mean = np.array(total_reward).mean()
            std = np.array(total_reward).std()
            total_reward = []
            solver_current = generator_current = 0
            print(f"{train_agent['name']}: Episode {episode}, Mean Reward {mean:.2f}, Std Reward {std:.2f}")

    torch.save(generator_model.policy_old.state_dict(), 'generator_model.pth')
    torch.save(solver_model.policy_old.state_dict(), 'solver_model.pth')

    env.close()

main()