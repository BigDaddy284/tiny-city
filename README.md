# The Tiny Town

Cosy isometric AI-driven village life simulation built with Unity 6 and local Ollama AI.

- Design: [GameDesignDocument.md](GameDesignDocument.md)
- Building, testing and downloading Windows builds: [Documentation/BuildAndTest.md](Documentation/BuildAndTest.md)

Status: Phase 3. Three residents, Mabel, Arthur and Nell, live in a small isometric village with three cottages, placeholder art and a repair workshop. Each has eight needs, a daily routine, and (for Mabel and Nell) a job. They meet by chance or go looking for each other, hold conversations of several turns, form relationships (friendship, trust, respect, dislike) and remember first meetings, disagreements and growing friendships. When a local Ollama service is running, the AI chooses their free-time activities, writes their conversation lines and their end-of-day reflections; the game checks every answer and decides turns, length and relationship changes itself. Without Ollama they follow their built-in routine and use simple small talk. Controls: Space pauses, keys 1–4 set the speed, F5 saves, F9 loads, the name buttons or N pick who to follow, Tab switches map and F follows the chosen resident.

- Ollama setup and troubleshooting: [Documentation/OllamaSetup.md](Documentation/OllamaSetup.md)

Manual test steps: [Documentation/PlaytestChecklist.md](Documentation/PlaytestChecklist.md)
