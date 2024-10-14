# **HyperRTS** – High-Performance RTS Engine for Unity using DOTS

HyperRTS is a highly modular and extensible real-time strategy (RTS) engine for Unity, built on top of Unity’s **Data-Oriented Technology Stack (DOTS)**. Designed to provide developers with the essential tools needed to create high-performance RTS games, HyperRTS leverages the **Entity Component System (ECS)**, **C# Job System**, and the **Burst Compiler** to ensure optimized gameplay even with large numbers of units, complex AI, and expansive maps.

## **Key Features**

- **High Performance**: Built with Unity DOTS to support large-scale RTS games with thousands of units and complex systems.
- **Modularity**: Engine components are modular, making it easy to extend or customize individual features such as unit management, building systems, resource management, AI, and more.
- **Entity Component System (ECS)**: Uses ECS architecture for optimal memory management and multithreaded processing.
- **Resource Management**: Easily define and manage in-game resources, including gathering, storing, and consuming them through customizable systems.
- **Unit Management**: Command and control your units with precision, including movement, combat, and formation systems.
- **Building System**: Place and upgrade structures dynamically during gameplay, with support for construction queues and worker units.
- **Pathfinding**: Efficient and scalable pathfinding using ECS-compatible algorithms.
- **Combat System**: Fully configurable combat mechanics, including health, damage, and targeting systems.
- **AI and Enemy Behavior**: Extendable AI systems to implement enemy logic, resource gathering, and combat strategies.
- **Fog of War**: Integrated fog of war system that updates dynamically based on unit positions and visibility.
- **Multiplayer Ready**: Built-in networking support, compatible with Unity’s DOTS NetCode.
- **Hybrid Rendering**: Supports Unity's Hybrid Renderer for rendering ECS-based entities.

## **Modularity**

HyperRTS is designed with modularity in mind. Each system (e.g., unit management, resource handling, AI) is isolated into its own module, making it easy to replace or extend specific functionalities.

## **Performance Optimization**

HyperRTS is optimized for performance through the use of DOTS, ECS, and multithreading. Here are some strategies used:

- **Job System**: All critical game logic is processed in parallel, leveraging Unity’s C# Job System.
- **Burst Compilation**: Systems are compiled to native code using the Burst Compiler, dramatically improving runtime performance.
- **Memory Optimization**: ECS promotes cache-friendly memory layout, reducing memory bottlenecks when dealing with large numbers of entities.

## **Getting Started with DOTS**

HyperRTS is built on DOTS, which may be unfamiliar to some developers. If you’re new to DOTS, here are some helpful resources:

- [Unity DOTS Overview](https://docs.unity3d.com/Packages/com.unity.entities@latest)
- [Unity ECS Manual](https://docs.unity3d.com/Packages/com.unity.entities@latest/manual/index.html)
- [Unity Job System](https://docs.unity3d.com/Manual/JobSystem.html)
