# The Tiny Town

Cosy isometric AI-driven village life simulation built with Unity 6 and local Ollama AI.

- Design: [GameDesignDocument.md](GameDesignDocument.md)
- Building, testing and downloading Windows builds: [Documentation/BuildAndTest.md](Documentation/BuildAndTest.md)

Status: Phase 4, build 1 of 3. Ten residents live in an isometric village with ten cottages, a general store, a café, a bakery, a clinic and a repair workshop, all in placeholder art. Each has eight needs, a daily routine, and either a job with set days, hours and an hourly wage, or a pension or allowance. Money is tracked to the penny: wages are paid when a shift ends, rent and pensions settle every Monday morning, and every payment is recorded. Cooking at home uses groceries bought at the store; the café and bakery sell meals, coffee and pastries during opening hours while stock lasts, and anyone broke and out of food can still snack, so nobody starves. Residents meet, chat over several turns, form relationships and remember what happened. When a local Ollama service is running, the AI chooses their free-time activities (seeing prices and their money), writes their conversation lines and their end-of-day reflections; the game checks every answer and keeps all the rules, including money. Without Ollama they follow their built-in routine and use simple small talk. Hobbies, health, ageing, romance and community events come in the next two Phase 4 builds. Controls: Space pauses, keys 1–4 set the speed, F5 saves, F9 loads, the name buttons or N pick who to follow, Tab switches map and F follows the chosen resident.

- Ollama setup and troubleshooting: [Documentation/OllamaSetup.md](Documentation/OllamaSetup.md)

Manual test steps: [Documentation/PlaytestChecklist.md](Documentation/PlaytestChecklist.md)
