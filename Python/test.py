import random

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


def test(env, solver, generator):
    env.reset()
    done = False

    while not done:
        decision_steps, terminal_steps = env.get_steps(solver['name'])
        done = len(terminal_steps.obs[0]) != 0
        if done:
            break

        state = np.array(get_observation(decision_steps.obs))

        action, log_prob = solver['model'].select_action(state)

        env.set_actions(solver['name'], ActionTuple(continuous=np.array([action.data.numpy()])))
        env.step()

        decision_steps, terminal_steps = env.get_steps(solver['name'])

        done = len(terminal_steps.obs[0]) != 0
        if done:
            break

        decision_steps, terminal_steps = env.get_steps(generator['name'])
        state = np.array(get_observation(decision_steps.obs))

        action, log_prob = generator['model'].select_action(state)

        env.set_actions(generator['name'], ActionTuple(continuous=np.array([action.data.numpy()])))
        env.step()

        done = len(terminal_steps.obs[0]) != 0

def main():
    file_path = 'Build/LevelDifficultyEstimation.exe'
    config_channel = EngineConfigurationChannel()
    config_channel.set_configuration_parameters(
        width=900, height=450, time_scale=2.0
    )
    env = UE(file_name=file_path, seed=random.randint(1, 999), side_channels=[config_channel], no_graphics=False)
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

    generator_model = PPO(state_size=generator_obs_size, action_size=generator_act_size)
    solver_model = PPO(state_size=solver_obs_size, action_size=solver_act_size)

    generator_model.policy_old.load_state_dict(torch.load('./generator_model.pth'))
    generator_model.policy.load_state_dict(torch.load('./generator_model.pth'))
    solver_model.policy_old.load_state_dict(torch.load('./solver_model.pth'))
    solver_model.policy.load_state_dict(torch.load('./solver_model.pth'))

    generator = {'name': generator_name, 'spec': generator_spec, 'model': generator_model}
    solver = {'name': solver_name, 'spec': solver_spec, 'model': solver_model}

    for episode in tqdm(range(10)):
        env.reset()
        test(env, solver, generator)

main()