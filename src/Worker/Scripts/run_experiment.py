import argparse
import sys
import time


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser()

    parser.add_argument("--experiment-id", required=True)
    parser.add_argument("--algorithm", required=True)
    parser.add_argument("--environment", required=True)
    parser.add_argument("--seed", type=int, required=True)
    parser.add_argument("--max-steps", type=int, required=True)
    parser.add_argument("--simulate-failure", action="store_true")

    return parser.parse_args()


def main() -> int:
    args = parse_arguments()

    print(
        f"Starting {args.algorithm} on {args.environment} "
        f"with seed={args.seed} and max_steps={args.max_steps}.",
        flush=True,
    )

    time.sleep(2)

    if args.simulate_failure:
        print(
            "Python experiment failed intentionally.",
            file=sys.stderr,
            flush=True,
        )
        return 1

    print(
        f"Python experiment {args.experiment_id} completed successfully.",
        flush=True,
    )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())